#!/usr/bin/env python3
"""Prove the complete Parties publication from bounded, observed registry bytes."""
from __future__ import annotations

import argparse
import base64
import hashlib
import http.client
import importlib.util
import io
import json
import os
import re
import socket
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BUILDS_SHA = "d9070aa0d43feb9e3359286bc797d2be65960727"
VALIDATOR_SHA256 = "0fff2281ecb9d8a46c48d65d1b0de58b724f2439138da43395004232e5942045"
IMAGES = ("parties", "parties-mcp", "parties-ui")
# A timed-out resolver cannot be interrupted. Its daemon worker retains one
# shared lease until resolution, candidate attempts and late socket cleanup settle.
_CONNECTION_WORK_LIMITER = threading.BoundedSemaphore(4)

VERSION = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$")


class PublicationError(Exception):
    """A support-safe rejection without request credentials or response bodies."""


def origin(url):
    """Normalize effective ports before deciding whether credentials may follow."""
    value = urllib.parse.urlsplit(url)
    return value.scheme.lower(), (value.hostname or "").lower(), value.port if value.port is not None else (443 if value.scheme.lower() == "https" else 80)


def redirect_headers(old_url, new_url, headers):
    """Keep same-origin credentials and reject all HTTPS downgrades."""
    old, new = urllib.parse.urlsplit(old_url), urllib.parse.urlsplit(new_url)
    if old.scheme.lower() == "https" and new.scheme.lower() != "https":
        raise PublicationError("HTTPS downgrade redirect rejected")
    if new.scheme.lower() not in {"http", "https"} or not new.hostname or new.username is not None or new.password is not None:
        raise PublicationError("Unsafe registry redirect rejected")
    return {key: value for key, value in headers.items()
            if key.lower() != "authorization" or origin(old_url) == origin(new_url)}


class DeadlineReader(io.RawIOBase):
    """Refresh the socket timeout before every receive, including HTTP header parsing."""

    def __init__(self, raw, network_socket, remaining):
        self.raw, self.network_socket, self.remaining = raw, network_socket, remaining

    def readable(self):
        return True

    def readinto(self, buffer):
        self.network_socket.settimeout(self.remaining())
        count = self.raw.readinto(buffer)
        self.remaining()
        return count

    def close(self):
        self.raw.close()
        super().close()


class DeadlineSocket:
    """Apply one absolute deadline across writes, headers and streaming response reads."""

    def __init__(self, network_socket, remaining):
        self.network_socket, self.remaining = network_socket, remaining

    def __getattr__(self, name):
        return getattr(self.network_socket, name)

    def sendall(self, value):
        self.network_socket.settimeout(self.remaining())
        self.network_socket.sendall(value)
        self.remaining()

    def makefile(self, mode):
        raw = self.network_socket.makefile(mode, buffering=0)
        return io.BufferedReader(DeadlineReader(raw, self.network_socket, self.remaining))


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def endpoint(value):
    parsed = urllib.parse.urlsplit(value)
    if (parsed.username is not None or parsed.password is not None or parsed.query or parsed.fragment
            or not parsed.hostname or parsed.scheme not in {"http", "https"}
            or parsed.scheme == "http" and parsed.hostname not in {"127.0.0.1", "localhost", "::1"}):
        raise PublicationError("Registry endpoint must use HTTPS (HTTP is limited to loopback fixtures)")
    return value.rstrip("/")


def save_json(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, indent=2, sort_keys=True) + "\n", encoding="utf-8")


class Downloader:
    """Finite retries and absolute per-request and total deadlines, including redirects."""

    def __init__(self, args, evidence):
        self.args = args
        self.evidence = evidence
        self.deadline = time.monotonic() + args.timeout_seconds

    def remaining(self):
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise PublicationError("Publication verification budget exhausted")
        return remaining

    def _connect(self, connection, remaining):
        limiter = _CONNECTION_WORK_LIMITER
        if not limiter.acquire(blocking=False):
            raise PublicationError("Connection work capacity is exhausted")
        ready, decision = threading.Event(), threading.Event()
        guard = threading.Lock()
        result = {"socket": None, "error": None, "claimed": False}

        def work():
            network_socket = None
            failure = None
            try:
                addresses = socket.getaddrinfo(connection.host, connection.port, type=socket.SOCK_STREAM)
                last_error = None
                for family, kind, protocol, _, address in addresses:
                    remaining()
                    network_socket = socket.socket(family, kind, protocol)
                    try:
                        # Recompute the remaining absolute budget for every address
                        # and again before TLS; DNS time never resets the deadline.
                        network_socket.settimeout(remaining())
                        if connection.source_address:
                            network_socket.bind(connection.source_address)
                        network_socket.connect(address)
                        network_socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                        network_socket.settimeout(remaining())
                        if isinstance(connection, http.client.HTTPSConnection):
                            network_socket = connection._context.wrap_socket(network_socket, server_hostname=connection.host)
                        remaining()
                        break
                    except OSError as error:
                        last_error = error
                        network_socket.close()
                        network_socket = None
                if network_socket is None:
                    raise last_error or OSError("Registry address could not be resolved")
            except BaseException as error:
                # Observe even late worker faults; they never escape a daemon thread.
                failure = error
            with guard:
                result.update(socket=network_socket, error=failure)
                ready.set()
            # Only the caller can claim a timely socket. An abandoned caller signals
            # immediately, so late completion/cleanup never blocks its verdict.
            decision.wait()
            try:
                if network_socket is not None and not result["claimed"]:
                    network_socket.close()
            except BaseException:
                pass  # Observe late cleanup faults without releasing a socket to the caller.
            finally:
                result["error"] = None
                result["socket"] = None
                limiter.release()

        try:
            threading.Thread(target=work, name="publication-connect", daemon=True).start()
        except BaseException:
            limiter.release()
            raise
        try:
            if not ready.wait(remaining()):
                raise TimeoutError("Absolute connection deadline exhausted")
            with guard:
                remaining()
                if result["error"] is not None:
                    raise result["error"]
                result["claimed"] = True
                return result["socket"]
        finally:
            decision.set()

    def get(self, url, target, artifact, headers=None, maximum=8 * 1024 * 1024):
        for attempt in range(1, self.args.attempts + 1):
            deadline = min(self.deadline, time.monotonic() + self.args.request_timeout_seconds)

            def remaining():
                self.remaining()
                value = deadline - time.monotonic()
                if value <= 0:
                    raise TimeoutError("Absolute request deadline exhausted")
                return value

            current, current_headers = url, dict(headers or {})
            try:
                for hop in range(6):
                    record = {"artifact": artifact, "attempt": attempt, "redirect": hop, "status": None}
                    self.evidence["downloads"].append(record)
                    body = bytearray()
                    connection = None
                    complete = False
                    try:
                        parsed = urllib.parse.urlsplit(current)
                        scheme = parsed.scheme.lower()
                        connection_type = http.client.HTTPSConnection if scheme == "https" else http.client.HTTPConnection
                        connection = connection_type(parsed.hostname, parsed.port, timeout=remaining())
                        connection.sock = self._connect(connection, remaining)
                        remaining()
                        connection.sock = DeadlineSocket(connection.sock, remaining)
                        path = urllib.parse.urlunsplit(("", "", parsed.path or "/", parsed.query, ""))
                        connection.request("GET", path, headers=current_headers)
                        with connection.getresponse() as response:
                            record["status"] = response.status
                            remaining()
                            if response.status in {301, 302, 303, 307, 308}:
                                location = response.getheader("Location")
                                if not location or hop == 5:
                                    raise PublicationError("Registry redirect limit or location is invalid")
                                redirected = urllib.parse.urljoin(current, location)
                                current_headers = redirect_headers(current, redirected, current_headers)
                                current = redirected
                                record["result"] = "redirect-body-not-read"
                                # Explicit close, never urllib's implicit unbounded redirect-body drain.
                                continue
                            if response.status != 200:
                                if response.status not in {404, 408, 425, 429, 500, 502, 503, 504}:
                                    raise PublicationError(f"Registry request rejected (HTTP {response.status})")
                                raise OSError("Transient registry status")
                            while True:
                                remaining()
                                chunk = response.read1(min(65536, maximum + 1 - len(body)))
                                if not chunk:
                                    if response.length not in (None, 0):
                                        raise OSError("Truncated registry response")
                                    break
                                body.extend(chunk)
                                if len(body) > maximum:
                                    raise PublicationError("Registry response exceeds the byte limit")
                            remaining()
                            target.parent.mkdir(parents=True, exist_ok=True)
                            target.write_bytes(body)
                            complete = True
                            return {"body": target.name, "content_type": response.getheader("Content-Type", ""),
                                    "docker_content_digest": response.getheader("Docker-Content-Digest")}
                    except (TimeoutError, OSError, http.client.HTTPException):
                        record["error"] = "transport-or-absolute-deadline"
                        raise
                    finally:
                        if connection is not None:
                            connection.close()
                        record.update(bytes=len(body), sha256=hashlib.sha256(body).hexdigest())
                        if not complete:
                            partial = target.with_name(f"{target.name}.attempt-{attempt}-redirect-{hop}.partial")
                            partial.parent.mkdir(parents=True, exist_ok=True)
                            prefix = body[:65536]
                            partial.write_bytes(prefix)
                            record.update(partial_file=partial.relative_to(self.args.evidence_directory).as_posix(),
                                          partial_bytes=len(prefix), partial_sha256=hashlib.sha256(prefix).hexdigest())
            except (TimeoutError, OSError, http.client.HTTPException):
                if attempt == self.args.attempts:
                    raise PublicationError("Registry propagation/transport retries exhausted") from None
            delay = min(self.args.retry_delay_seconds, self.remaining())
            if delay:
                time.sleep(delay)
        raise AssertionError("Unreachable retry state")


def verify_images(args, downloader, validator, evidence):
    registry_url = endpoint(args.registry_url)
    registry = urllib.parse.urlsplit(registry_url).netloc
    if urllib.parse.urlsplit(registry_url).path:
        raise PublicationError("OCI registry endpoint cannot contain a path")
    username, key = os.environ.get("HEXALITH_ZOT_USERNAME", ""), os.environ.get("HEXALITH_ZOT_API_KEY", "")
    if not username or not key:
        raise PublicationError("Read credentials for the OCI registry are required")
    authorization = "Basic " + base64.b64encode(f"{username}:{key}".encode()).decode("ascii")
    labels = validator.expected_provenance_labels(args.repository, args.source_sha, args.version)
    for image in IMAGES:
        directory = args.evidence_directory / "containers" / image
        base = f"{registry_url}/v2/{image}"
        manifest_headers = {"Authorization": authorization, "Accept": validator.MANIFEST_ACCEPT}
        objects = {}

        def manifest(reference, filename):
            return downloader.get(f"{base}/manifests/{urllib.parse.quote(reference, safe=':')}",
                                  directory / filename, image + ":manifest", manifest_headers)

        tag = manifest(args.version, "tag.raw")
        digest = validator._validate_digest(tag.get("docker_content_digest"))
        objects[digest] = manifest(digest, "immutable-index.raw")
        index = json.loads((directory / "tag.raw").read_bytes())
        descriptors = index.get("manifests") if isinstance(index, dict) else None
        if not isinstance(descriptors, list):
            raise PublicationError("OCI index manifests must be an array")
        validator._validate_platforms(descriptors)
        for position, descriptor in enumerate(descriptors):
            child_digest = validator._validate_digest(descriptor.get("digest"))
            objects[child_digest] = manifest(child_digest, f"child-{position}.raw")
            child = json.loads((directory / f"child-{position}.raw").read_bytes())
            config = child.get("config") if isinstance(child, dict) else None
            config_digest = validator._validate_digest(config.get("digest") if isinstance(config, dict) else None)
            objects[config_digest] = downloader.get(
                f"{base}/blobs/{config_digest}", directory / f"config-{position}.raw", image + ":config",
                {"Authorization": authorization}, maximum=2 * 1024 * 1024)
        save_json(directory / "responses.json", {"tag": tag, "objects": objects})
        validated = validator.validate_capture(directory, labels)
        receipt = validator.write_evidence(directory, f"{registry}/{image}:{args.version}", validated)
        evidence["images"].append({"image": image, "index_digest": receipt["index_digest"],
                                   "platforms": receipt["platforms"]})


def verify(args, evidence):
    if not VERSION.fullmatch(args.version) or not re.fullmatch("[0-9a-f]{40}", args.source_sha):
        raise PublicationError("Release version and source SHA must be exact")
    if args.repository != "Hexalith/Hexalith.Parties":
        raise PublicationError("Publication repository must be Hexalith/Hexalith.Parties")
    validator_bytes = args.oci_validator.read_bytes()
    if hashlib.sha256(validator_bytes).hexdigest() != VALIDATOR_SHA256:
        raise PublicationError("OCI validator does not match the immutable Builds execution source")
    validator = load_module("parties_pinned_oci_validator", args.oci_validator)
    packages = load_module("parties_package_validator", Path(__file__).with_name("validate-nuget-packages.py"))
    inventory = packages.load_package_inventory(args.package_manifest)
    commons_version = packages.resolve_hexalith_commons_version()
    event_store_version = packages.resolve_hexalith_event_store_version()
    dependency_versions = {"Hexalith.Commons.Http": commons_version, "Hexalith.Commons.UniqueIds": commons_version}
    evidence["dependency_versions"] = {**dependency_versions, "Hexalith.EventStore.*": event_store_version}

    nuget_url = endpoint(args.nuget_base_url)
    downloader = Downloader(args, evidence)
    repository_url = f"https://github.com/{args.repository}"
    for package_id in inventory:
        lower = package_id.lower()
        filename = f"{lower}.{args.version.lower()}.nupkg"
        path = args.evidence_directory / "nuget" / filename
        downloader.get(f"{nuget_url}/{lower}/{args.version.lower()}/{filename}", path, package_id,
                                  maximum=64 * 1024 * 1024)
        packages.validate_published_metadata(path, package_id, args.version, repository_url, args.source_sha,
                                             dependency_versions, event_store_version)
        evidence["packages"].append({"id": package_id, "version": args.version,
                                     "repository": repository_url, "repository_type": "git",
                                     "repository_commit": args.source_sha, "bytes": path.stat().st_size,
                                     "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                     "file": f"nuget/{filename}"})
    verify_images(args, downloader, validator, evidence)
    downloader.remaining()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--repository", default="Hexalith/Hexalith.Parties")
    parser.add_argument("--package-manifest", type=Path, default=Path("tools/release-packages.json"))
    parser.add_argument("--oci-validator", type=Path, required=True)
    parser.add_argument("--evidence-directory", type=Path, required=True)
    parser.add_argument("--nuget-base-url", default="https://api.nuget.org/v3-flatcontainer")
    parser.add_argument("--registry-url", default="https://" + os.environ.get("HEXALITH_ZOT_REGISTRY", "registry.hexalith.com"))
    parser.add_argument("--attempts", type=int, default=4)
    parser.add_argument("--retry-delay-seconds", type=float, default=2)
    parser.add_argument("--request-timeout-seconds", type=float, default=30)
    parser.add_argument("--timeout-seconds", type=float, default=600)
    args = parser.parse_args()
    if (not 1 <= args.attempts <= 6 or not 0 <= args.retry_delay_seconds <= 30
            or not 0 < args.request_timeout_seconds <= 30 or not 0 < args.timeout_seconds <= 900):
        parser.error("Download and retry bounds are outside the supported finite range")
    evidence = {"result": "fail", "version": args.version, "source_sha": args.source_sha,
                "builds_commit": BUILDS_SHA, "oci_validator_sha256": VALIDATOR_SHA256,
                "hash_scope": "observed registry bytes; no equivalence to unsigned build archives is asserted",
                "packages": [], "images": [], "downloads": []}
    try:
        verify(args, evidence)
        evidence["result"] = "pass"
    except Exception as error:
        # Only allow known support-safe diagnostics. XML, archive and transport errors
        # can include untrusted remote data, local paths or request details.
        evidence["error"] = str(error) if isinstance(error, PublicationError) else "Artifact metadata, archive or OCI graph validation failed"
        if type(error).__module__ == "parties_pinned_oci_validator":
            evidence["error_code"] = error.code
        print("Publication verification failed: " + evidence["error"], file=sys.stderr)
    finally:
        save_json(args.evidence_directory / "publication-verification.json", evidence)
    if evidence["result"] != "pass":
        return 1
    print(f"Validated {len(evidence['packages'])} packages and {len(evidence['images'])} two-platform images for {args.version} at {args.source_sha}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
