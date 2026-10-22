"""Run the real publication CLI and immutable Builds OCI validator against isolated HTTP fixtures."""
import base64
import hashlib
import io
import json
import os
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import zipfile
import zlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

SOURCE = "a" * 40
VERSION = "1.2.3"
BUILDS = "397c94a4e246c90b21cf408790fa0d55bf32d795"
INDEX = "application/vnd.oci.image.index.v1+json"
MANIFEST = "application/vnd.oci.image.manifest.v1+json"
CONFIG = "application/vnd.oci.image.config.v1+json"


def data(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def digest(value):
    return "sha256:" + hashlib.sha256(value).hexdigest()


def stored_zip(entries):
    """Build a stored zip. A declared size can exceed the bytes actually stored."""
    local = bytearray()
    central = bytearray()
    offset = 0
    for name, payload, declared in entries:
        raw_name = name.encode()
        payload = payload or b""
        size = len(payload) if declared is None else declared
        checksum = zlib.crc32(payload) & 0xFFFFFFFF
        local_header = struct.pack("<IHHHHHIIIHH", 0x04034B50, 20, 0, 0, 0, 0, checksum, len(payload), size, len(raw_name), 0)
        local += local_header + raw_name + payload
        central_header = struct.pack(
            "<IHHHHHHIIIHHHHHII", 0x02014B50, 20, 20, 0, 0, 0, 0, checksum, len(payload), size,
            len(raw_name), 0, 0, 0, 0, 0, offset)
        central += central_header + raw_name
        offset += len(local_header) + len(raw_name) + len(payload)
    eocd = struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, len(entries), len(entries), len(central), offset, 0)
    return bytes(local) + bytes(central) + eocd


def run(repository, scenario):
    repository = repository.resolve()
    catalog = json.loads(subprocess.check_output([
        "dotnet", "msbuild", str(repository / "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj"),
        "-nologo", "-p:Configuration=Release", "-getProperty:HexalithCommonsVersion,HexalithEventStoreVersion"], text=True))["Properties"]
    commons_version, event_store_version = catalog["HexalithCommonsVersion"], catalog["HexalithEventStoreVersion"]
    inventory = json.loads((repository / "tools/release-packages.json").read_text())
    package_ids = [entry["id"] for entry in inventory["packages"]]
    routes, requests, counts = {}, [], {}
    package_target = "/nuget/" + package_ids[0].lower() + f"/{VERSION}/" + package_ids[0].lower() + f".{VERSION}.nupkg"
    for package_id in package_ids:
        affected = package_id == package_ids[0]
        attributes = {"id": package_id, "version": VERSION, "readme": "README.md", "license": "MIT"}
        repo = {"type": "git", "url": "https://github.com/Hexalith/Hexalith.Parties", "commit": SOURCE}
        if affected:
            for case, key, value in [("wrong-id", "id", package_ids[1]), ("wrong-version", "version", "1.2.4")]:
                if scenario == case:
                    attributes[key] = value
            for case, key, value in [("wrong-repository", "url", "https://example.invalid/other"),
                                      ("wrong-repository-type", "type", "svn"), ("wrong-commit", "commit", "b" * 40)]:
                if scenario == case:
                    repo[key] = value
            if scenario == "missing-license":
                del attributes["license"]
        if affected and scenario in {"valid-file-license", "missing-file-license", "uppercase-file-license"}:
            attributes["license"] = "ABSENT.txt" if scenario == "uppercase-file-license" else "LICENSE.txt"
        if affected and scenario in {"valid-legacy-file-license", "missing-legacy-file-license"}:
            del attributes["license"]
            attributes["licenseFile"] = "LICENSE.txt"
        license_kind = "File" if affected and scenario == "uppercase-file-license" else None
        fields = "".join((f'<license type="{license_kind or ("file" if value == "LICENSE.txt" else "expression")}">{value}</license>'
                          if key == "license" else f"<{key}>{value}</{key}>") for key, value in attributes.items())
        if affected and scenario == "ambiguous-metadata":
            fields += f"<id>{package_id}</id>"
        dependency_values = [("Hexalith.EventStore.Contracts", event_store_version),
                             ("Hexalith.Commons.Http", commons_version), ("Hexalith.Commons.UniqueIds", commons_version)]
        if affected and scenario == "forbidden-dependency":
            dependency_values.append(("Hexalith.Parties", "1.2.3"))
        if affected and scenario == "no-dependencies":
            dependency_values = []
        elif affected and scenario == "missing-eventstore-dependency":
            dependency_values = [item for item in dependency_values if item[0] != "Hexalith.EventStore.Contracts"]
        elif affected and scenario in {"wrong-eventstore-version", "wrong-commons-version"}:
            changed = "Hexalith.EventStore.Contracts" if scenario == "wrong-eventstore-version" else "Hexalith.Commons.UniqueIds"
            dependency_values = [(name, "0.0.1" if name == changed else version) for name, version in dependency_values]
        elif scenario == "missing-required-commons-http" and package_id == "Hexalith.Parties.Client":
            dependency_values = [item for item in dependency_values if item[0] != "Hexalith.Commons.Http"]
        elif affected and scenario == "wrong-extra-eventstore-version":
            dependency_values.append(("Hexalith.EventStore.Client", "0.0.1"))
        dependencies = '<dependencies><group targetFramework="net10.0">' + "".join(
            f'<dependency id="{name}" version="{version}" />' for name, version in dependency_values) + '</group></dependencies>'
        nuspec = ('<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>' + fields
                  + '<repository ' + " ".join(f'{key}="{value}"' for key, value in repo.items()) + ' />'
                  + dependencies + '</metadata></package>').encode()
        if affected and scenario == "nested-metadata":
            nuspec = nuspec.replace(b"<metadata>", b"<wrapper><metadata>").replace(b"</metadata>", b"</metadata></wrapper>")
        elif affected and scenario == "nested-id":
            nuspec = nuspec.replace(f"<id>{package_id}</id>".encode(), f"<wrapper><id>{package_id}</id></wrapper>".encode())
        elif affected and scenario == "duplicate-repository":
            nuspec = nuspec.replace(b"</metadata>", b'<repository type="git" url="https://example.invalid/other" commit="' + (b"b" * 40) + b'" /></metadata>')
        elif affected and scenario == "non-package-root":
            nuspec = nuspec.replace(b"<package ", b"<bundle ", 1).replace(b"</package>", b"</bundle>")
        elif affected and scenario == "nuspec-doctype":
            nuspec = b'<!DOCTYPE package [<!ENTITY x "x">]>' + nuspec
        memory = io.BytesIO()
        with zipfile.ZipFile(memory, "w") as archive:
            archive.writestr(("lib/" if affected and scenario == "nested-nuspec" else "") + package_id + ".nuspec", nuspec)
            if affected and scenario in {"valid-file-license", "valid-legacy-file-license"}:
                archive.writestr("LICENSE.txt", "fixture license")
            if not affected or scenario != "missing-readme":
                archive.writestr("README.md", "fixture")
            if affected and scenario == "duplicate-nuspec":
                archive.writestr("second.nuspec", nuspec)
            if affected and scenario == "duplicate-member":
                archive.writestr("readme.md", "ambiguous")
        if affected and scenario == "malformed-package":
            package = b"malformed archive"
        elif affected and scenario == "member-count-bound":
            package = stored_zip([(package_id + ".nuspec", nuspec, None), ("README.md", b"fixture", None),
                                  *[(f"extra-{index}.txt", b"", None) for index in range(9999)]])
        elif affected and scenario == "archive-size-bound":
            package = stored_zip([(package_id + ".nuspec", nuspec, None), ("README.md", b"fixture", None),
                                  ("pad.bin", b"", 256 * 1024 * 1024 + 1)])
        elif affected and scenario == "nuspec-size-bound":
            package = stored_zip([(package_id + ".nuspec", nuspec, 1024 * 1024 + 1), ("README.md", b"fixture", None)])
        else:
            package = memory.getvalue()
        if affected and scenario == "corrupt-package-crc":
            package = package.replace(b"fixture", b"fixturf", 1)
        lower = package_id.lower()
        routes[f"/nuget/{lower}/{VERSION}/{lower}.{VERSION}.nupkg"] = (200, "application/octet-stream", package, None)

    labels = {"org.opencontainers.image.source": "https://github.com/Hexalith/Hexalith.Parties",
              "org.opencontainers.image.url": f"https://github.com/Hexalith/Hexalith.Parties/releases/tag/v{VERSION}",
              "org.opencontainers.image.documentation": f"https://github.com/Hexalith/Hexalith.Parties/blob/{SOURCE}/README.md",
              "org.opencontainers.image.revision": SOURCE, "org.opencontainers.image.version": VERSION}
    for image in ("parties", "parties-mcp", "parties-ui"):
        descriptors = []
        for architecture in ("amd64", "arm64"):
            child_labels = dict(labels)
            if image == "parties-ui" and architecture == "arm64" and scenario == "wrong-provenance":
                child_labels["org.opencontainers.image.revision"] = "b" * 40
            platform = "s390x" if image == "parties" and architecture == "arm64" and scenario == "wrong-config-platform" else architecture
            config = data({"os": "linux", "architecture": platform, "config": {"Labels": child_labels}})
            config_digest = digest(config)
            routes[f"/v2/{image}/blobs/{config_digest}"] = (200, CONFIG, config + (b" " if scenario == "wrong-config-digest" and image == "parties" else b""), config_digest)
            child = data({"schemaVersion": 2, "mediaType": MANIFEST, "config": {"mediaType": CONFIG, "digest": config_digest, "size": len(config)}, "layers": []})
            child_digest = digest(child)
            routes[f"/v2/{image}/manifests/{child_digest}"] = (200, MANIFEST, child + (b" " if scenario == "wrong-child-digest" and image == "parties" else b""), child_digest)
            descriptors.append({"mediaType": MANIFEST, "digest": child_digest, "size": len(child), "platform": {"os": "linux", "architecture": architecture}})
        if image == "parties" and scenario == "missing-platform":
            descriptors.pop()
        if image == "parties" and scenario == "duplicate-platform":
            descriptors[1]["platform"]["architecture"] = "amd64"
        index = data({"schemaVersion": 2, "mediaType": INDEX, "manifests": descriptors})
        if image == "parties" and scenario == "malformed-index":
            index = b"{}"
        elif image == "parties" and scenario == "oversized-index":
            index = b" " * (8 * 1024 * 1024 + 1)
        index_digest = digest(index)
        immutable = index + b" " if image == "parties" and scenario == "immutable-mismatch" else index
        routes[f"/v2/{image}/manifests/{index_digest}"] = (200, INDEX, immutable, index_digest)
        tag_digest = "sha256:" + "0" * 64 if image == "parties" and scenario == "wrong-index-digest" else index_digest
        # Keep a resolvable immutable lookup for this deliberately incorrect reported digest.
        routes[f"/v2/{image}/manifests/{tag_digest}"] = (200, INDEX, immutable, tag_digest)
        routes[f"/v2/{image}/manifests/{VERSION}"] = (200, INDEX, index, tag_digest)
    transient = package_target if scenario in {"propagation-package", "transient-recovery", "transport-recovery"} else f"/v2/parties-ui/manifests/{VERSION}"
    release = threading.Event()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            requests.append(self.path)
            counts[self.path] = counts.get(self.path, 0) + 1
            status, content_type, body, sha = routes.get(self.path, (404, "text/plain", b"missing", None))
            if self.path.startswith("/v2/"):
                expected = "Basic " + base64.b64encode(b"fixture-user:fixture-secret").decode()
                if self.headers.get("Authorization") != expected or scenario == "oci-authentication":
                    status = 401
            elif self.headers.get("Authorization"):
                status = 401
            if self.path == package_target:
                if scenario == "missing-package":
                    status = 404
                elif scenario == "authentication":
                    status = 401
                elif scenario == "forbidden":
                    status = 403
                elif scenario == "retry-exhaustion":
                    status = 503
                elif scenario in {"request-timeout", "total-budget"}:
                    release.wait(10)
            if scenario.startswith("missing-image-") and self.path == f"/v2/{scenario[len('missing-image-'):]}/manifests/{VERSION}":
                status = 404
            if scenario in {"propagation-package", "propagation-image", "transient-recovery"} and self.path == transient and counts[self.path] == 1:
                status = 503 if scenario == "transient-recovery" else 404
            if scenario == "transport-recovery" and self.path == transient and counts[self.path] == 1:
                self.connection.shutdown(socket.SHUT_RDWR)
                self.connection.close()
                return
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            if sha:
                self.send_header("Docker-Content-Digest", sha)
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            try:
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError):
                pass

    with tempfile.TemporaryDirectory(prefix="parties-publication-") as temporary:
        root = Path(temporary)
        validator = root / "oci_registry_validator.py"
        validator.write_bytes(subprocess.check_output(["git", "-C", str(repository / "references/Hexalith.Builds"),
                                                       "show", f"{BUILDS}:Github/publish-containers/oci_registry_validator.py"]))
        if scenario == "unpinned-validator":
            validator.write_bytes(validator.read_bytes() + b"\n# modified\n")
        if scenario == "partial-inventory":
            inventory["packages"].pop()
        elif scenario == "duplicate-inventory":
            inventory["packages"].append(inventory["packages"][0])
        elif scenario == "ambiguous-inventory":
            inventory["packages"][1]["id"] = package_ids[0].lower()
        manifest = root / "packages.json"
        manifest.write_text('{"packages":[],"packages":' + json.dumps(inventory["packages"]) + '}'
                            if scenario == "duplicate-json-inventory" else json.dumps(inventory))
        evidence = root / "evidence"
        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
        thread.start()
        endpoint = f"http://127.0.0.1:{server.server_port}"
        command = [sys.executable, str(repository / "scripts/verify-release-publication.py"), "--version", VERSION,
                   "--source-sha", SOURCE, "--package-manifest", str(manifest), "--oci-validator", str(validator),
                   "--evidence-directory", str(evidence), "--nuget-base-url", endpoint + "/nuget", "--registry-url", endpoint,
                   "--attempts", "2", "--retry-delay-seconds", "0", "--request-timeout-seconds", "0.15", "--timeout-seconds", "0.03" if scenario == "total-budget" else "10"]
        env = dict(os.environ, HEXALITH_ZOT_USERNAME="fixture-user", HEXALITH_ZOT_API_KEY="fixture-secret")
        try:
            result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=15)
        finally:
            release.set()
            server.shutdown()
            server.server_close()
            thread.join()
        receipt = json.loads((evidence / "publication-verification.json").read_text())
        expected = scenario in {"valid", "propagation-package", "propagation-image", "transient-recovery", "transport-recovery", "valid-file-license", "valid-legacy-file-license"}
        assert (result.returncode == 0) == expected, (scenario, result.stdout, result.stderr, receipt)
        assert receipt["result"] == ("pass" if expected else "fail")
        if expected:
            assert len(receipt["packages"]) == 9 and len(receipt["images"]) == 3
            for package in receipt["packages"]:
                downloaded = (evidence / package["file"]).read_bytes()
                assert package["sha256"] == hashlib.sha256(downloaded).hexdigest()
                assert package["bytes"] == len(downloaded)
            for image in receipt["images"]:
                assert image["platforms"] == ["linux/amd64", "linux/arm64"]
                directory = evidence / "containers" / image["image"]
                oci = json.loads((directory / "oci-validation.json").read_text())
                assert oci["index_digest"] == digest((directory / "index.raw").read_bytes())
                for child in oci["children"]:
                    for kind in ("manifest", "config"):
                        assert child[f"{kind}_raw_sha256"] == hashlib.sha256((directory / child[f"{kind}_raw_file"]).read_bytes()).hexdigest()
            if scenario in {"propagation-package", "propagation-image", "transient-recovery", "transport-recovery"}:
                assert counts[transient] == 2
        elif scenario in {"missing-package", "retry-exhaustion", "request-timeout"}:
            assert counts[package_target] == 2
        elif scenario == "total-budget":
            assert counts[package_target] == 1
            assert receipt["error"] == "Publication verification budget exhausted"
        elif scenario in {"authentication", "forbidden"}:
            assert counts[package_target] == 1
        elif scenario in {"partial-inventory", "duplicate-inventory", "ambiguous-inventory", "duplicate-json-inventory", "unpinned-validator"}:
            assert not requests
        elif scenario in {"member-count-bound", "archive-size-bound", "nuspec-size-bound", "duplicate-repository",
                          "non-package-root", "nested-nuspec", "uppercase-file-license", "nuspec-doctype"}:
            assert receipt["error"] == "Artifact metadata, archive or OCI graph validation failed", receipt
        text_evidence = result.stdout + result.stderr + "".join(path.read_text() for path in evidence.rglob("*.json"))
        assert "fixture-secret" not in text_evidence and "fixture-user" not in text_evidence
        print(scenario + ": verified " + ("complete pass" if expected else "fail-closed rejection"))
        print(f"Retained {len(receipt['downloads'])} bounded download observations; validated {len(receipt['packages'])} packages and {len(receipt['images'])} images.")


def exercise_connection(verifier, scenario):
    import math
    import time
    from types import SimpleNamespace

    with tempfile.TemporaryDirectory(prefix="parties-connect-") as temporary:
        root = Path(temporary)
        original_socket, original_time = verifier.socket, verifier.time
        original_limiter = verifier._CONNECTION_WORK_LIMITER
        original_hook = threading.excepthook
        limiter = threading.BoundedSemaphore(1)
        verifier._CONNECTION_WORK_LIMITER = limiter
        unhandled = []
        threading.excepthook = lambda value: unhandled.append(value.exc_type.__name__)
        entered, release = threading.Event(), threading.Event()
        close_entered, close_release, closed = threading.Event(), threading.Event(), threading.Event()
        sockets, resolver_calls, timeouts = [], [], []
        clock = [0.0]
        candidate_case = scenario.startswith("download-candidate-")

        class Candidate:
            def __init__(self, *_):
                self.index, self.closed = len(sockets), False
                sockets.append(self)

            def settimeout(self, value):
                timeouts.append((self.index, value))

            def bind(self, _):
                pass

            def setsockopt(self, *_):
                pass

            def connect(self, _):
                if scenario == "download-late-socket":
                    entered.set()
                    release.wait(3)
                    return
                if self.index == 0:
                    clock[0] += 0.04 if scenario == "download-candidate-remaining-budget" else 0.06
                    raise OSError("synthetic first candidate failure")
                clock[0] += 0.02

            def close(self):
                if scenario == "download-late-socket":
                    close_entered.set()
                    close_release.wait(3)
                self.closed = True
                closed.set()

        def resolve(*_, **__):
            resolver_calls.append(True)
            if scenario == "download-resolver-stall":
                entered.set()
                release.wait(3)
                raise OSError("synthetic late resolver fault")
            return [(socket.AF_INET, socket.SOCK_STREAM, socket.IPPROTO_TCP, "", ("127.0.0.1", port))
                    for port in (1, 2)]

        verifier.socket = SimpleNamespace(getaddrinfo=resolve, socket=Candidate, SOCK_STREAM=socket.SOCK_STREAM,
                                          IPPROTO_TCP=socket.IPPROTO_TCP, TCP_NODELAY=socket.TCP_NODELAY)
        if candidate_case:
            verifier.time = SimpleNamespace(monotonic=lambda: clock[0], sleep=time.sleep)
        args = SimpleNamespace(timeout_seconds=0.2 if candidate_case else 0.05,
                               request_timeout_seconds=0.05, attempts=1, retry_delay_seconds=0,
                               evidence_directory=root / "evidence")
        evidence = {"downloads": []}
        downloader = verifier.Downloader(args, evidence)
        verdict, done = [], threading.Event()

        def query():
            try:
                downloader.get("http://registry.invalid/object", args.evidence_directory / "body.raw", "fixture")
            except Exception as error:
                verdict.append(error)
            finally:
                done.set()

        query_thread = threading.Thread(target=query, daemon=True)
        acquired = False
        try:
            query_thread.start()
            if not candidate_case:
                assert entered.wait(0.5), "Connection work never entered its controlled gate"
            assert done.wait(0.5), "Connection waiting exceeded the budget while its resolver/socket gate remained closed"
            assert len(verdict) == 1 and isinstance(verdict[0], verifier.PublicationError), verdict
            assert str(verdict[0]) in {"Registry propagation/transport retries exhausted", "Publication verification budget exhausted"}
            if candidate_case:
                expected = 2 if scenario == "download-candidate-remaining-budget" else 1
                assert len(sockets) == expected, (scenario, len(sockets))
                assert all(item.closed for item in sockets), "Deadline-exhausted candidate sockets must close"
                assert math.isclose(timeouts[0][1], 0.05, abs_tol=1e-9), timeouts
                if expected == 2:
                    assert math.isclose(next(value for index, value in timeouts if index == 1), 0.01, abs_tol=1e-9), timeouts
            else:
                assert not release.is_set()
                assert not limiter.acquire(blocking=False), "Abandoned connection work released its lease early"
                for index in range(5):
                    saturated = verifier.Downloader(args, {"downloads": []})
                    try:
                        saturated.get("http://registry.invalid/object", args.evidence_directory / f"saturated-{index}.raw", "fixture")
                    except verifier.PublicationError as error:
                        assert str(error) == "Connection work capacity is exhausted", str(error)
                    else:
                        raise AssertionError("Saturation started additional resolver/socket work")
                assert len(resolver_calls) == 1
                release.set()
                if scenario == "download-late-socket":
                    assert close_entered.wait(0.5), "The socket that connected late was not closed"
                    assert not closed.is_set()
                    assert not limiter.acquire(blocking=False), "Lease must remain occupied through late socket cleanup"
                    close_release.set()
            acquired = limiter.acquire(timeout=0.5)
            assert acquired, "Connection capacity did not recover after late work/fault cleanup"
            limiter.release()
            acquired = False
            assert not unhandled, unhandled
            assert all(item.closed for item in sockets)
            for record in evidence["downloads"]:
                prefix = (args.evidence_directory / record["partial_file"]).read_bytes()
                assert record["status"] is None and record["bytes"] == 0
                assert record["sha256"] == hashlib.sha256(prefix).hexdigest()
            assert "synthetic" not in json.dumps(evidence)
        finally:
            release.set()
            close_release.set()
            query_thread.join(1)
            if acquired:
                limiter.release()
            verifier.socket, verifier.time = original_socket, original_time
            verifier._CONNECTION_WORK_LIMITER = original_limiter
            threading.excepthook = original_hook

        # Actual loopback recovery proves the observed capacity can be reused.
        class Recovery(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                self.send_response(200)
                self.send_header("Content-Length", "8")
                self.end_headers()
                self.wfile.write(b"complete")

        server = ThreadingHTTPServer(("127.0.0.1", 0), Recovery)
        thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
        thread.start()
        verifier._CONNECTION_WORK_LIMITER = limiter
        try:
            recovery_args = SimpleNamespace(timeout_seconds=1, request_timeout_seconds=0.5, attempts=1,
                                            retry_delay_seconds=0, evidence_directory=root / "recovery")
            target = recovery_args.evidence_directory / "body.raw"
            verifier.Downloader(recovery_args, {"downloads": []}).get(
                f"http://127.0.0.1:{server.server_port}/object", target, "fixture")
            assert target.read_bytes() == b"complete"
        finally:
            server.shutdown()
            server.server_close()
            thread.join()
            verifier._CONNECTION_WORK_LIMITER = original_limiter
        print(scenario + ": bounded resolver/candidate waits, retained capacity, observed late faults and closed late sockets verified")


def exercise_download(repository, scenario):
    import importlib.util
    import ssl
    import time
    from types import SimpleNamespace

    module_path = repository.resolve() / "scripts/verify-release-publication.py"
    spec = importlib.util.spec_from_file_location("publication_download_fixture", module_path)
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    authorization = {"Authorization": "Bearer fixture-secret"}
    if scenario in {"download-resolver-stall", "download-late-socket", "download-candidate-remaining-budget", "download-candidate-exhausted-budget"}:
        exercise_connection(verifier, scenario)
        return
    if scenario == "download-default-ports":
        assert verifier.origin("https://registry.example/a") == verifier.origin("https://registry.example:443/b")
        assert verifier.redirect_headers("https://registry.example/a", "https://registry.example:443/b", authorization) == authorization
        print(scenario + ": implicit and explicit default HTTPS ports retain same-origin credentials")
        return
    with tempfile.TemporaryDirectory(prefix="parties-download-") as temporary:
        root = Path(temporary)
        stopped = threading.Event()
        observed = []
        destination_observed = []
        destination = None
        destination_thread = None

        class Destination(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                destination_observed.append(self.headers.get("Authorization"))
                self.send_response(200)
                self.send_header("Content-Length", "8")
                self.end_headers()
                self.wfile.write(b"complete")

        if scenario in {"download-two-origin-auth", "download-https-downgrade", "download-unsafe-redirect"}:
            destination = ThreadingHTTPServer(("127.0.0.1", 0), Destination)
            destination_thread = threading.Thread(target=destination.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
            destination_thread.start()

        class Origin(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                observed.append((self.path, self.headers.get("Authorization")))
                try:
                    if scenario == "download-unsafe-redirect":
                        self.send_response(302)
                        location = (f"http://user:secret@127.0.0.1:{destination.server_port}/stolen"
                                    if self.path == "/userinfo" else f"ftp://127.0.0.1:{destination.server_port}/stolen")
                        self.send_header("Location", location)
                        self.send_header("Content-Length", "0")
                        self.end_headers()
                        return
                    if scenario == "download-slow-headers":
                        self.wfile.write(b"HTTP/1.1 200 OK\r\nX-Slow: ")
                        self.wfile.flush()
                        for _ in range(100):
                            self.wfile.write(b"a")
                            self.wfile.flush()
                            if stopped.wait(0.01):
                                break
                        self.wfile.write(b"\r\nContent-Length: 8\r\n\r\ncomplete")
                        return
                    if scenario == "download-redirect-deadline":
                        stopped.wait(0.04)
                    if self.path != "/done" and scenario != "download-slow-body":
                        self.send_response(302)
                        location = f"http://127.0.0.1:{destination.server_port}/done" if destination else "/done"
                        if scenario == "download-redirect-deadline":
                            hop = int(self.path.rsplit("/", 1)[-1])
                            location = f"/hop/{hop + 1}" if hop < 3 else "/done"
                        self.send_header("Location", location)
                        self.send_header("Content-Length", "1000000000")
                        self.end_headers()
                        if scenario == "download-redirect-body":
                            for _ in range(150):
                                self.wfile.write(b"discard-me")
                                self.wfile.flush()
                                if stopped.wait(0.01):
                                    break
                        return
                    self.send_response(200)
                    self.send_header("Content-Length", "1000000" if scenario == "download-slow-body" else "8")
                    self.end_headers()
                    if scenario == "download-slow-body":
                        for _ in range(150):
                            self.wfile.write(b"x" * 1024)
                            self.wfile.flush()
                            if stopped.wait(0.01):
                                break
                    else:
                        self.wfile.write(b"complete")
                except (BrokenPipeError, ConnectionResetError, ssl.SSLError):
                    pass

        server = ThreadingHTTPServer(("127.0.0.1", 0), Origin)
        old_ca = os.environ.get("SSL_CERT_FILE")
        if scenario in {"download-https-downgrade", "download-uppercase-https"}:
            certificate, key = root / "ca.pem", root / "key.pem"
            subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1",
                            "-subj", "/CN=localhost", "-addext", "subjectAltName=DNS:localhost,IP:127.0.0.1",
                            "-keyout", str(key), "-out", str(certificate)], check=True, capture_output=True)
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            context.load_cert_chain(certificate, key)
            server.socket = context.wrap_socket(server.socket, server_side=True)
            os.environ["SSL_CERT_FILE"] = str(certificate)
        thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
        thread.start()
        args = SimpleNamespace(timeout_seconds=1, request_timeout_seconds=0.1, attempts=2,
                               retry_delay_seconds=0, evidence_directory=root / "evidence")
        evidence = {"downloads": []}
        downloader = verifier.Downloader(args, evidence)
        scheme = "HTTPS" if scenario == "download-uppercase-https" else "https" if scenario == "download-https-downgrade" else "http"
        path = "/hop/0" if scenario == "download-redirect-deadline" else "/start"
        started = time.monotonic()
        failure = None
        unsafe_failures = []
        try:
            if scenario == "download-unsafe-redirect":
                for unsafe_path in ("/userinfo", "/scheme"):
                    try:
                        downloader.get(f"http://127.0.0.1:{server.server_port}{unsafe_path}",
                                       args.evidence_directory / f"{unsafe_path.strip('/')}.raw", "fixture", authorization)
                    except verifier.PublicationError as error:
                        unsafe_failures.append(str(error))
            else:
                downloader.get(f"{scheme}://127.0.0.1:{server.server_port}{path}", args.evidence_directory / "body.raw", "fixture", authorization)
        except verifier.PublicationError as error:
            failure = str(error)
        finally:
            elapsed = time.monotonic() - started
            stopped.set()
            server.shutdown()
            server.server_close()
            thread.join()
            if destination:
                destination.shutdown()
                destination.server_close()
                destination_thread.join()
            if old_ca is None:
                os.environ.pop("SSL_CERT_FILE", None)
            else:
                os.environ["SSL_CERT_FILE"] = old_ca
        if scenario in {"download-slow-body", "download-slow-headers", "download-redirect-deadline"}:
            assert failure == "Registry propagation/transport retries exhausted", (scenario, failure, evidence)
            assert elapsed < 0.6, (scenario, elapsed)
        elif scenario == "download-https-downgrade":
            assert failure == "HTTPS downgrade redirect rejected", (failure, evidence)
            assert not destination_observed
        elif scenario == "download-unsafe-redirect":
            assert unsafe_failures == ["Unsafe registry redirect rejected", "Unsafe registry redirect rejected"], unsafe_failures
            assert destination_observed == []
            assert [item[0] for item in observed] == ["/userinfo", "/scheme"]
        else:
            assert failure is None, (failure, evidence)
            assert (args.evidence_directory / "body.raw").read_bytes() == b"complete"
            assert elapsed < 0.5, (scenario, elapsed)
        if scenario in {"download-same-origin-auth", "download-uppercase-https"}:
            assert observed == [("/start", authorization["Authorization"]), ("/done", authorization["Authorization"])]
        elif scenario == "download-two-origin-auth":
            assert destination_observed == [None]
        if scenario == "download-redirect-body":
            redirect = evidence["downloads"][0]
            assert redirect["status"] == 302 and redirect["bytes"] == 0
        for record in evidence["downloads"]:
            if "partial_file" in record:
                prefix = (args.evidence_directory / record["partial_file"]).read_bytes()
                assert len(prefix) == record["partial_bytes"] <= 65536
                assert hashlib.sha256(prefix).hexdigest() == record["partial_sha256"]
                if scenario == "download-slow-body":
                    assert record["status"] == 200 and record["bytes"] > 0
                    assert record["sha256"] == hashlib.sha256(prefix).hexdigest()
        assert "fixture-secret" not in json.dumps(evidence)
        print(scenario + ": bounded headers/body/redirects and retained support-safe byte receipts verified")


if __name__ == "__main__":
    if sys.argv[2].startswith("download-"):
        exercise_download(Path(sys.argv[1]), sys.argv[2])
    else:
        run(Path(sys.argv[1]), sys.argv[2])
