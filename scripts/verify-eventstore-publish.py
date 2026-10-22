#!/usr/bin/env python3
"""Qualify the actual owner AppHost's local manifest without retaining credentials."""

import argparse
import ctypes
from contextlib import contextmanager
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import signal
import subprocess
import tempfile
import time
import sys
from urllib.parse import unquote, urlsplit


AUTHORITY = "https://identity.example.test/realms/publish-qualification"
AUDIENCES = ["qualification-primary", "qualification-secondary"]
TOKEN_ENDPOINT = AUTHORITY + "/protocol/openid-connect/token"
ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PROJECT = ROOT / "references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"
REFERENCE = re.compile(r"\{([a-zA-Z0-9_-]+)\.value\}\Z")
SENSITIVE = re.compile(r"(?:password|username|signingkey|client[_-]?secret|api[_-]?key|token)\Z", re.IGNORECASE)


URL = re.compile(r"[a-z][a-z0-9+.-]*://[^\s\"<>]+", re.IGNORECASE)
CREDENTIAL_ASSIGNMENT = re.compile(
    r"(?:^|[;\s])(?:password|pwd|user\s*id|uid|username|client[_ -]?secret|api[_ -]?key|token)\s*=\s*([^;\s]+)", re.IGNORECASE)


class QualificationError(Exception):
    """A bounded diagnostic whose text never contains artifact values."""


def require(condition, message):
    if not condition:
        raise QualificationError(message)


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key, child in value.items():
            yield str(key)
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def numeric_address(host):
    """Parse standard IPs and inet-style numeric IPv4 without resolving DNS."""
    try:
        address = ipaddress.ip_address(host)
        return getattr(address, "ipv4_mapped", None) or address
    except ValueError:
        parts = host.split(".")
        if not 1 <= len(parts) <= 4:
            return None
        values = []
        for part in parts:
            if not re.fullmatch(r"(?:0[xX][0-9a-fA-F]+|[0-9]+)", part):
                return None
            try:
                values.append(int(part, 16 if part.lower().startswith("0x") else 8 if len(part) > 1 and part[0] == "0" else 10))
            except ValueError:
                return None
        widths = [8] * (len(values) - 1) + [8 * (5 - len(values))]
        number = 0
        for value, width in zip(values, widths):
            if value >= 1 << width:
                return None
            number = (number << width) | value
        return ipaddress.IPv4Address(number)


def is_development_endpoint(value):
    if "{security." in value or "{local-auth-" in value or value == "hexalith-dev":
        return True
    for candidate in re.findall(r"https?://[^\s\"<>]+", value, re.IGNORECASE):
        try:
            parsed = urlsplit(candidate)
            host = (parsed.hostname or "").rstrip(".").lower()
            if host in {"localhost", "security", "keycloak", "host.docker.internal"} or host.endswith((".localhost", ".local")):
                return True
            address = numeric_address(host)
            if address is not None and (address.is_loopback or address.is_unspecified):
                return True
        except ValueError:
            return True
    return False


def validate_manifest(manifest, poison_values, grants):
    """Reject literals and local auth before checking the complete external contract."""
    resources = manifest.get("resources", {})
    require(isinstance(resources, dict) and bool(resources), "manifest resources missing")
    for value in strings(manifest):
        require(not any(poison in value for poison in poison_values), "credential poison found in emitted manifest")
        require(not is_development_endpoint(value), "development endpoint found in emitted manifest")
    require(not any(name == "security" or name.startswith("local-auth-") for name in resources), "local authentication resource published")

    def reference(value, expected, secret=False):
        match = REFERENCE.fullmatch(value or "")
        require(match is not None and match[1] == expected, "authentication parameter reference missing")
        parameter = resources.get(expected, {})
        require(parameter.get("type") == "parameter.v0", "authentication reference does not resolve to a parameter")
        require(parameter.get("value") == f"{{{expected}.inputs.value}}", "authentication parameter input expression missing")
        value_input = parameter.get("inputs", {}).get("value", {})
        if secret:
            require(value_input.get("secret") is True, "authentication secret reference is not secret")
        require("default" not in value_input, "authentication parameter contains a literal default")

    def credential(value):
        match = REFERENCE.fullmatch(value)
        require(match is not None, "literal credential found in emitted manifest")
        reference(value, match[1], secret=True)

    for value in strings(manifest):
        for match in CREDENTIAL_ASSIGNMENT.finditer(value):
            credential(match[1].strip("\"'"))
        for candidate in URL.findall(value):
            try:
                parsed = urlsplit(candidate)
                for part in (parsed.username, parsed.password):
                    if part:
                        credential(unquote(part))
            except ValueError as error:
                raise QualificationError("malformed emitted URL") from error

    for name, resource in resources.items():
        arguments = resource.get("args", [])
        for index, argument in enumerate(arguments):
            if argument.startswith("-"):
                flag, separator, value = argument.partition("=")
                if SENSITIVE.search(flag):
                    credential(value if separator else arguments[index + 1] if index + 1 < len(arguments) else "")
        for key, value in resource.get("env", {}).items():
            if SENSITIVE.search(key) and value:
                match = REFERENCE.fullmatch(value)
                require(match is not None, "literal credential found in emitted environment")
                reference(value, match[1], secret=True)
        if resource.get("type") == "parameter.v0":
            value_input = resource.get("inputs", {}).get("value", {})
            if value_input.get("secret"):
                require("default" not in value_input, "secret parameter contains a literal default")
                require(resource.get("value") == f"{{{name}.inputs.value}}", "secret parameter input expression missing")

    for name in ("eventstore", "eventstore-admin", "sample", "sample-api"):
        env = resources.get(name, {}).get("env", {})
        prefix = "Authentication__JwtBearer__"
        require(env.get(prefix + "Authority") == AUTHORITY and env.get(prefix + "Issuer") == AUTHORITY,
                "external JWT authority or issuer missing")
        require(env.get(prefix + "Audience") == AUDIENCES[0], "primary JWT audience missing")
        audiences = [env[key] for key in sorted(env) if key.startswith(prefix + "ValidAudiences__")]
        require(audiences == AUDIENCES, "JWT audience set changed")
        algorithms = [env[key] for key in sorted(env) if key.startswith(prefix + "AllowedAlgorithms__")]
        require(algorithms == ["RS256"], "external JWT algorithm set changed")
        require(env.get(prefix + "RequireHttpsMetadata") == "true" and env.get(prefix + "SigningKey") == "",
                "external JWT validation weakened")

    workload = resources["eventstore"]["env"]
    reference(workload.get("Authentication__WorkloadIssuer__ClientId"), "external-eventstore-workload-client-id")
    reference(workload.get("Authentication__WorkloadIssuer__ClientSecret"), "external-eventstore-workload-client-secret", secret=True)
    require(resources["sample"]["env"].get("EventStore__DomainService__AppId") == "sample", "workload receiver identity missing")
    for name, role in (("sample-blazor-ui", "sample"), ("eventstore-admin-ui", "admin")):
        env = resources.get(name, {}).get("env", {})
        prefix = "EventStore__Authentication__"
        require(env.get(prefix + "Authority") == AUTHORITY and env.get(prefix + "Issuer") == AUTHORITY,
                "external UI authority or issuer missing")
        require(env.get(prefix + "Audience") == AUDIENCES[0] and env.get(prefix + "TokenEndpoint") == TOKEN_ENDPOINT,
                "external UI audience or token endpoint missing")
        algorithms = [env[key] for key in sorted(env) if key.startswith(prefix + "AllowedAlgorithms__")]
        require(env.get(prefix + "RequireHttpsMetadata") == "true" and algorithms == ["RS256"],
                "external UI validation weakened")
        require(env.get(prefix + "GrantType") == grants[role] and env.get(prefix + "Scope") == "openid qualification",
                "external UI grant or scope missing")
        require(env.get(prefix + "SigningKey") == "", "UI symmetric signing key published")
        reference(env.get(prefix + "ClientId"), f"external-{role}-auth-client-id")
        for field in ("Username", "Password", "ClientSecret"):
            selected = field in (("Username", "Password") if grants[role] == "password" else ("ClientSecret",))
            value = env.get(prefix + field)
            if selected:
                reference(value, f"external-{role}-auth-{field.lower().replace('clientsecret', 'client-secret')}", secret=True)
            else:
                require(value == "", "unused authentication credential published")


@contextmanager
def owned_child_reaper():
    """Linux qualification only: adopt/reap descendants from our command groups."""
    require(sys.platform == "linux", "owned publisher command lifetime requires Linux")
    libc = ctypes.CDLL(None, use_errno=True)
    previous = ctypes.c_int()
    require(libc.prctl(37, ctypes.byref(previous), 0, 0, 0) == 0
            and libc.prctl(36, 1, 0, 0, 0) == 0, "owned publisher reaper could not initialize")
    try:
        yield
    finally:
        require(libc.prctl(36, previous.value, 0, 0, 0) == 0, "owned publisher reaper could not restore")


def stop_owned_command(process):
    # start_new_session makes this group exclusive to the launched command. Even
    # an exited parent can leave an inherited stdout pipe open in a descendant.
    deadline = time.monotonic() + 5
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=max(0.001, deadline - time.monotonic()))
        while True:
            try:
                child, _ = os.waitpid(-process.pid, os.WNOHANG)
            except ChildProcessError:
                return
            if child == 0:
                require(time.monotonic() < deadline, "owned publisher descendants did not exit within cleanup deadline")
                time.sleep(0.01)
    except subprocess.TimeoutExpired as error:
        raise QualificationError("owned publisher command did not exit within cleanup deadline") from error


def run_private(command, directory, environment, timeout):
    with owned_child_reaper():
        process = None
        try:
            process = subprocess.Popen(command, cwd=directory, env=environment, stdout=subprocess.PIPE,
                                       stderr=subprocess.STDOUT, start_new_session=True)
            process.communicate(timeout=timeout)
            require(process.returncode == 0, f"owner publisher command failed (exit {process.returncode}); output withheld")
        except (OSError, subprocess.TimeoutExpired) as error:
            raise QualificationError("owner publisher could not complete within its observation deadline") from error
        finally:
            if process is not None:
                try:
                    stop_owned_command(process)
                finally:
                    process.stdout.close()


@contextmanager
def emitted_manifests(project=DEFAULT_PROJECT, build=True):
    """Publish with poison inputs in a private lifetime and remove every raw artifact."""
    project = project.resolve()
    require(project.is_file(), "owner AppHost project missing")
    environment = {key: value for key, value in os.environ.items()
                   if not key.lower().startswith(("authentication__", "parameters__"))
                   and key not in {"PUBLISH_TARGET", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT"}}
    environment.update({"DOTNET_ENVIRONMENT": "Production", "ASPNETCORE_ENVIRONMENT": "Production",
                        "SKIP_PREREQUISITE_CHECK": "true"})
    if build:
        run_private(["dotnet", "build", str(project), "--disable-build-servers", "-c", "Release", "-m:1", "-p:NuGetAudit=false",
                     "-p:MinVerVersionOverride=1.0.0"], ROOT, environment, 300)
    assembly = project.parent / "bin/Release/net10.0" / (project.stem + ".dll")
    require(assembly.is_file(), "built owner AppHost assembly missing")
    environment.update({"Authentication__JwtBearer__Authority": AUTHORITY, "Authentication__JwtBearer__Issuer": AUTHORITY,
                        "Authentication__JwtBearer__Audience": AUDIENCES[0],
                        "Authentication__JwtBearer__ValidAudiences__0": AUDIENCES[1],
                        "Authentication__JwtBearer__AllowedAlgorithms__0": "RS256",
                        "Authentication__JwtBearer__TokenEndpoint": TOKEN_ENDPOINT})
    poison = ["publish-poison-" + secrets.token_hex(24) for _ in range(6)]
    for key, value in zip(("Authentication__JwtBearer__SigningKey", "Parameters__local-auth-signing-key",
                           "Parameters__local-auth-admin-password", "Parameters__local-auth-sample-password",
                           "Parameters__local-auth-workload-client-secret", "Parameters__external-eventstore-workload-client-secret"), poison):
        environment[key] = value
    with tempfile.TemporaryDirectory(prefix="parties-owner-publish-") as private:
        directory = Path(private)
        directory.chmod(0o700)
        results = []
        for index, grants in enumerate((dict(sample="client_credentials", admin="password"),
                                        dict(sample="password", admin="client_credentials"))):
            for role in ("sample", "admin"):
                environment[f"Authentication__JwtBearer__{role.title()}Ui__GrantType"] = grants[role]
                environment[f"Authentication__JwtBearer__{role.title()}Ui__Scope"] = "openid qualification"
                for field in ("password", "username", "client-secret"):
                    environment[f"Parameters__external-{role}-auth-{field}"] = poison[2]
            path = directory / f"manifest-{index}.json"
            # Invoke the installed SDK's actual manifest publisher, not an in-memory model.
            # This is a local publish operation; it never starts or deploys the topology.
            run_private(["dotnet", str(assembly), "--publisher", "manifest", "--output-path", str(path)],
                        project.parent, environment, 120)
            path.chmod(0o600)
            raw = path.read_bytes()
            manifest = json.loads(raw)
            validate_manifest(manifest, poison, grants)
            results.append((manifest, poison, grants, hashlib.sha256(raw).hexdigest()))
        yield results, directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--no-build", action="store_true", help="Use the existing Release owner assembly")
    args = parser.parse_args()
    try:
        with emitted_manifests(args.project, build=not args.no_build) as (manifests, directory):
            receipt = {"publisher": "Aspire SDK manifest", "manifests": [
                {"sha256": digest, "grants": grants, "resources": len(manifest["resources"])}
                for manifest, _, grants, digest in manifests]}
        require(not directory.exists(), "private publish output was not removed")
        receipt["private_output_removed"] = True
        print(json.dumps(receipt, indent=2))
        return 0
    except QualificationError as error:
        print(f"EventStore publish qualification failed: {error}; credential-bearing output withheld.")
        return 1
    except (OSError, ValueError, KeyError, TypeError):
        print("EventStore publish qualification failed; credential-bearing output withheld.")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
