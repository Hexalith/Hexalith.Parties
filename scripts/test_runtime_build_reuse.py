"""Exercise the production marker gate before any SDK command can run."""
import hashlib
import json
import os
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import tempfile
import time
import unittest

PREPARE = Path(__file__).with_name('prepare-aspire-runtime-hosts.sh')


class RuntimeBuildReuseTests(unittest.TestCase):
    def test_invalid_completion_cannot_reach_the_sdk_or_reuse_an_old_prepared_manifest(self):
        with tempfile.TemporaryDirectory(prefix='parties-reuse-marker-') as temporary:
            root = Path(temporary)
            tools = root/'tools'
            tools.mkdir()
            invoked = root/'dotnet-invoked'
            dotnet = tools/'dotnet'
            dotnet.write_text(f'#!{sys.executable}\nfrom pathlib import Path\nPath({str(invoked)!r}).write_text("unexpected")\nraise SystemExit(91)\n')
            dotnet.chmod(0o755)
            artifacts = root/'artifacts'
            profile = artifacts/'runtime-profile/Release'
            profile.mkdir(parents=True)
            started = profile/'solution-build-started-ns'
            completed = profile/'solution-build-completed.json'
            prepared = profile/'prepared.json'
            now = time.time_ns()
            valid = dict(configuration='Release', buildStartedNs=now-1000, completedNs=now-1,
                         attempt='test', solution=str(PREPARE.resolve().parents[1]/'Hexalith.Parties.Standalone.slnx'),
                         sourceSha256='0'*64, projects={}, additionalInputs=[], additionalSha256='0'*64)
            cases = {
                'legacy-marker': json.dumps({key: valid[key] for key in ('configuration', 'buildStartedNs', 'completedNs')}),
                'missing-completion': None,
                'malformed-json': '{',
                'non-object': '[]',
                'wrong-configuration': json.dumps(dict(valid,configuration='Debug')),
                'missing-start-time': json.dumps({k:v for k,v in valid.items() if k!='buildStartedNs'}),
                'boolean-start': json.dumps(dict(valid,buildStartedNs=True)),
                'boolean-completion': json.dumps(dict(valid,completedNs=True)),
                'zero-start': json.dumps(dict(valid,buildStartedNs=0)),
                'string-time': json.dumps(dict(valid,completedNs=str(now-1))),
                'completion-before-start': json.dumps(dict(valid,completedNs=now-2000)),
                'future-completion': json.dumps(dict(valid,completedNs=now+3600_000_000_000)),
                'different-begin': json.dumps(dict(valid,buildStartedNs=now-2000)),
                'unknown-field': json.dumps(dict(valid,unverified=True)),
                'duplicate-field': json.dumps(valid)[:-1]+',"configuration":"Release"}',
                'missing-begin': json.dumps(valid),
                'linked-completion': json.dumps(valid),
                'linked-begin': json.dumps(valid),
            }
            for name,body in cases.items():
                with self.subTest(name=name):
                    completed.unlink(missing_ok=True)
                    started.unlink(missing_ok=True)
                    started.write_text(str(valid['buildStartedNs'])+'\n')
                    if body is not None:
                        completed.write_text(body)
                    if name=='missing-begin':
                        started.unlink()
                    if name=='linked-completion':
                        target=root/'complete.json';target.write_text(body)
                        completed.unlink();completed.symlink_to(target)
                    if name=='linked-begin':
                        target=root/'begin.txt';target.write_text(started.read_text())
                        started.unlink();started.symlink_to(target)
                    prepared.write_text('{"old":"cannot authorize reuse"}')
                    result=subprocess.run(['bash',str(PREPARE),'--reuse-build','true','--artifacts-path',str(artifacts)],
                        env=dict(os.environ,PATH=str(tools)+os.pathsep+os.environ['PATH']),capture_output=True,text=True,timeout=10)
                    self.assertNotEqual(0,result.returncode,result.stdout+result.stderr)
                    self.assertIn('build',result.stderr.lower())
                    self.assertFalse(invoked.exists(),result.stdout+result.stderr)
                    self.assertFalse(prepared.exists())


    def test_build_and_preparation_refuse_source_and_profile_drift(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            scripts = root / "scripts"
            scripts.mkdir()
            for name in ("runtime-build-marker.py", "validate-aspire-runtime-cohort.py", PREPARE.name):
                shutil.copy(PREPARE.with_name(name), scripts)
            solution = root / "Hexalith.Parties.Standalone.slnx"
            solution.write_text("<Solution/>")
            source = root / "src/Program.cs"
            source.parent.mkdir()
            source.write_text("class Program {}")
            imported = root / "Directory.Build.props"
            imported.write_text("<Project/>")
            marker = [sys.executable, str(scripts / "runtime-build-marker.py")]
            args = ["--configuration", "Release", "--attempt", "first"]
            profile = root / "artifacts/runtime-profile/Release"
            subprocess.run([*marker, "begin", *args], check=True)
            source.write_text("class Changed {}")
            failed = subprocess.run([*marker, "complete", *args], capture_output=True, text=True)
            self.assertIn("build inputs changed", failed.stderr)
            self.assertNotEqual(failed.returncode, 0)
            self.assertFalse((profile / "solution-build-completed.json").exists())

            target = root / "src/bin/Release/Program.dll"
            target.parent.mkdir(parents=True)
            target.write_bytes(b"first build")
            target.with_suffix(".deps.json").write_text("{}")
            assets = root / "src/obj/project.assets.json"
            assets.parent.mkdir()
            assets.write_text("{}")
            project = root / "src/Program.csproj"
            project.write_text("<Project/>")
            args[-1] = "second"
            subprocess.run([*marker, "begin", *args], check=True)
            attempt = profile / "attempts/second"
            obsolete = profile / "attempts/interrupted"
            obsolete.mkdir()
            (obsolete / "truncated.output").write_text("")
            copied = target.parent / "Configuration/settings.json"
            copied.parent.mkdir()
            copied.write_text("{}")
            settings_source = root / "settings-source"
            settings_source.write_text("{}")
            second_source = root / "other-settings"
            second_source.write_text("another owner's settings")
            (attempt / "project.output").write_text(f"{project}\n{target}\n{assets}\n"
                f"{settings_source}|Configuration/settings.json\n{second_source}|Configuration/settings.json\n")
            external = root / "linked-input"
            external.write_text("original")
            checksum = hashlib.sha256(external.read_bytes()).hexdigest()
            (attempt / "project.inputs").write_text(f"{checksum} {external}\n")
            external.unlink()
            disappeared = subprocess.run([*marker, "complete", *args], capture_output=True, text=True)
            self.assertNotEqual(disappeared.returncode, 0)
            self.assertFalse((profile / "solution-build-completed.json").exists())
            external.write_text("original")
            settings_source.write_text("new configuration")
            stale_copy = subprocess.run([*marker, "complete", *args], capture_output=True, text=True)
            self.assertNotEqual(stale_copy.returncode, 0)
            self.assertIn("deployed copy differs", stale_copy.stderr)
            self.assertFalse((profile / "solution-build-completed.json").exists())
            settings_source.write_text("{}")
            subprocess.run([*marker, "complete", *args], check=True)
            subprocess.run([*marker, "verify", *args], check=True, stdout=subprocess.DEVNULL)
            cohort = runpy.run_path(str(scripts / "validate-aspire-runtime-cohort.py"))
            completed = profile / "solution-build-completed.json"
            marker_data = json.loads(completed.read_text())
            metadata = dict(Configuration="Release", HexalithEventStoreVersion="5.4.0", TargetFramework="net11.0",
                            HexalithPartiesRuntimeTargetFramework="net11.0", CustomAfterMicrosoftCommonProps=str(cohort["RUNTIME_PROFILE"]),
                            TargetPath=str(target), ProjectAssetsFile=str(assets))
            for output in (target, assets, copied, target.with_name("Added.dll")):
                with self.subTest(output=output):
                    prior = output.read_bytes() if output.exists() else None
                    output.write_bytes(b"changed")
                    with self.assertRaisesRegex(ValueError, "differs from the completed solution build"):
                        cohort["record"](metadata, project, "Release", "5.4.0", marker_data["buildStartedNs"],
                                         "runtime", {}, build_marker=completed)
                    if prior is None:
                        output.unlink()
                    else:
                        output.write_bytes(prior)
            for path in (source, imported, source.with_name("Added.cs"), external):
                with self.subTest(path=path):
                    original = path.read_bytes() if path.exists() else None
                    path.write_text("changed")
                    prepared = profile / "prepared.json"
                    prepared.write_text("{}")
                    result = subprocess.run(["bash", str(scripts / PREPARE.name), "--reuse-build", "true"],
                                            capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn("build inputs changed", result.stderr)
                    self.assertFalse(prepared.exists())
                    if original is None:
                        path.unlink()
                    else:
                        path.write_bytes(original)
            prepared.write_text("{}")
            subprocess.run([*marker, "begin", "--configuration", "Release", "--attempt", "third"], check=True)
            self.assertFalse(prepared.exists())
            candidate = profile / "candidate.json"
            candidate.write_text("{}")
            stale = subprocess.run([*marker, "verify", "--configuration", "Release", "--expected-start",
                                    str(marker_data["buildStartedNs"]), "--publish-manifest", str(candidate)],
                                   capture_output=True, text=True)
            self.assertNotEqual(stale.returncode, 0)
            self.assertFalse(prepared.exists())


if __name__=='__main__':
    unittest.main()
