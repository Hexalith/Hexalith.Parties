"""Exercise both real owner emissions and mutations of those emitted bytes."""

import copy
from contextlib import redirect_stdout
import io
import json
import os
import sys
import tempfile
import time
from unittest import mock
import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location("eventstore_publish", Path(__file__).with_name("verify-eventstore-publish.py"))
publisher = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(publisher)


class OwnerPublishTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with publisher.emitted_manifests() as (results, directory):
            cls.results = results
            cls.private_directory = directory

    def test_real_owner_manifests_preserve_external_contract_for_both_grants(self):
        self.assertEqual(len(self.results), 2)
        for manifest, poison, grants, _ in self.results:
            publisher.validate_manifest(manifest, poison, grants)
        self.assertFalse(self.private_directory.exists())

    def test_emitted_copy_rejects_poison_anywhere(self):
        for manifest, poison, grants, _ in self.results:
            mutated = copy.deepcopy(manifest)
            mutated["resources"]["eventstore"]["env"]["unrelated"] = poison[0]
            with self.assertRaisesRegex(publisher.QualificationError, "credential poison"):
                publisher.validate_manifest(mutated, poison, grants)

    def test_emitted_copy_rejects_literal_credentials_and_nonsecret_references(self):
        manifest, poison, grants, _ = self.results[0]
        for value in ("synthetic-literal-credential", "{external-eventstore-workload-client-id.value}"):
            mutated = copy.deepcopy(manifest)
            mutated["resources"]["eventstore"]["env"]["Authentication__WorkloadIssuer__ClientSecret"] = value
            with self.assertRaises(publisher.QualificationError):
                publisher.validate_manifest(mutated, poison, grants)

    def test_emitted_copy_rejects_development_endpoints(self):
        manifest, poison, grants, _ = self.results[0]
        for endpoint in ("http://2130706433/token", "http://0x7f000001/token", "http://127.1/token",
                         "http://127.0.1/token", "http://0177.0.0.1/token", "http://localhost:8080/token", "https://127.0.0.1/realms/dev", "http://[::1]/token",
                         "http://security:8080/realms/hexalith", "{security.bindings.http.url}/realms/hexalith"):
            mutated = copy.deepcopy(manifest)
            mutated["resources"]["eventstore-admin-ui"]["env"]["EventStore__Authentication__TokenEndpoint"] = endpoint
            with self.assertRaisesRegex(publisher.QualificationError, "development endpoint"):
                publisher.validate_manifest(mutated, poison, grants)

    def test_secret_parameter_rejects_top_level_literal_value(self):
        manifest, poison, grants, _ = self.results[0]
        mutated = copy.deepcopy(manifest)
        mutated["resources"]["external-eventstore-workload-client-secret"]["value"] = "synthetic-literal"
        with self.assertRaisesRegex(publisher.QualificationError, "input expression"):
            publisher.validate_manifest(mutated, poison, grants)

    def test_credentials_in_args_connection_strings_and_url_userinfo(self):
        manifest, poison, grants, _ = self.results[0]
        secret = "{external-eventstore-workload-client-secret.value}"
        for field, value in (("args", ["--password", "synthetic-literal"]),
                             ("args", ["--client-secret=synthetic-literal"]),
                             ("connectionString", "Server=database.example.test;Password=synthetic-literal;"),
                             ("connectionString", "Server=database.example.test;User ID=synthetic-user;"),
                             ("endpoint", "https://synthetic-user:synthetic-password@identity.example.test/token")):
            with self.subTest(field=field, value=value):
                mutated = copy.deepcopy(manifest)
                mutated["resources"]["eventstore"][field] = value
                with self.assertRaisesRegex(publisher.QualificationError, "literal credential"):
                    publisher.validate_manifest(mutated, poison, grants)
        for field, value in (("args", ["--password", secret]), ("args", ["--client-secret=" + secret]),
                             ("connectionString", "Server=database.example.test;Password=" + secret + ";"),
                             ("endpoint", "https://" + secret + ":" + secret + "@identity.example.test/token")):
            mutated = copy.deepcopy(manifest)
            mutated["resources"]["eventstore"][field] = value
            publisher.validate_manifest(mutated, poison, grants)

    def test_failed_emissions_remove_private_output_and_keep_diagnostics_bounded(self):
        marker = "synthetic-credential-must-never-appear"
        for failure in ("publication", "malformed-json", "validation", "second-emission"):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as workspace:
                project = Path(workspace) / "Owner.csproj"
                project.touch()
                assembly = project.parent / "bin/Release/net10.0/Owner.dll"
                assembly.parent.mkdir(parents=True)
                assembly.touch()
                private_paths = []
                emissions = []
                real_temporary_directory = tempfile.TemporaryDirectory

                def private_directory(*args, **kwargs):
                    directory = real_temporary_directory(*args, **kwargs)
                    private_paths.append(Path(directory.name))
                    return directory

                def emit(command, directory, environment, timeout):
                    if "--output-path" not in command:
                        return
                    path = Path(command[command.index("--output-path") + 1])
                    index = len(emissions)
                    emissions.append(path)
                    if failure == "publication" or failure == "second-emission" and index == 1:
                        path.write_text(marker)
                        raise publisher.QualificationError("owner publisher command failed; output withheld")
                    if failure == "malformed-json":
                        path.write_text(marker)
                    else:
                        manifest = copy.deepcopy(self.results[index][0])
                        if failure == "validation":
                            manifest["resources"]["eventstore"]["env"]["Authentication__WorkloadIssuer__ClientSecret"] = marker
                        path.write_text(json.dumps(manifest))

                output = io.StringIO()
                with mock.patch.object(publisher.tempfile, "TemporaryDirectory", private_directory), \
                     mock.patch.object(publisher, "run_private", side_effect=emit), \
                     mock.patch.object(sys, "argv", ["verify-eventstore-publish", "--project", str(project)]), \
                     redirect_stdout(output):
                    self.assertEqual(publisher.main(), 1)
                self.assertEqual(len(emissions), 2 if failure == "second-emission" else 1)
                self.assertTrue(private_paths)
                self.assertTrue(all(not path.exists() for path in private_paths))
                self.assertNotIn(marker, output.getvalue())
                self.assertNotIn(workspace, output.getvalue())
                self.assertLess(len(output.getvalue()), 300)

    def test_emitted_copy_rejects_audience_workload_and_secret_contract_mutations(self):
        manifest, poison, grants, _ = self.results[0]
        mutations = [
            lambda r: r["sample"]["env"].pop("Authentication__JwtBearer__ValidAudiences__1"),
            lambda r: r["eventstore"]["env"].pop("Authentication__WorkloadIssuer__ClientSecret"),
            lambda r: r["external-admin-auth-password"]["inputs"]["value"].update(secret=False),
            lambda r: r["external-eventstore-workload-client-secret"]["inputs"]["value"].update(default="synthetic-literal"),
            lambda r: r["sample-api"]["env"].update(Authentication__JwtBearer__RequireHttpsMetadata="false"),
        ]
        for mutate in mutations:
            mutated = copy.deepcopy(manifest)
            mutate(mutated["resources"])
            with self.assertRaises(publisher.QualificationError):
                publisher.validate_manifest(mutated, poison, grants)


@unittest.skipUnless(sys.platform == "linux", "Owned command supervision is qualified on Linux only")
class OwnedCommandTests(unittest.TestCase):
    def test_timeout_reaps_an_inherited_pipe_descendant_after_parent_exit(self):
        with tempfile.TemporaryDirectory() as private:
            child_pid = Path(private) / "child.pid"
            marker = "synthetic-private-command-output"
            code = ("import subprocess,sys,pathlib; "
                    "p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); "
                    "pathlib.Path(sys.argv[1]).write_text(str(p.pid)); "
                    "print(sys.argv[2],flush=True)")
            started = time.monotonic()
            with self.assertRaises(publisher.QualificationError) as failure:
                publisher.run_private([sys.executable, "-c", code, str(child_pid), marker], private, os.environ.copy(), 1)
            self.assertLess(time.monotonic() - started, 7)
            self.assertFalse(Path("/proc", child_pid.read_text()).exists())
            self.assertNotIn(marker, str(failure.exception))
            self.assertLess(len(str(failure.exception)), 200)

    def test_failed_command_reaps_its_descendants_and_withholds_output(self):
        with tempfile.TemporaryDirectory() as private:
            child_pid = Path(private) / "child.pid"
            code = ("import subprocess,sys,pathlib; "
                    "p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); "
                    "pathlib.Path(sys.argv[1]).write_text(str(p.pid)); print('synthetic-secret',flush=True); sys.exit(7)")
            with self.assertRaises(publisher.QualificationError) as failure:
                publisher.run_private([sys.executable, "-c", code, str(child_pid)], private, os.environ.copy(), 2)
            self.assertFalse(Path("/proc", child_pid.read_text()).exists())
            self.assertIn("exit 7", str(failure.exception))
            self.assertNotIn("synthetic-secret", str(failure.exception))


if __name__ == "__main__":
    unittest.main()
