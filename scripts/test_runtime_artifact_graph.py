"""Verify the actual package profile survives SDK solution-reference traversal."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class RuntimeArtifactGraphTests(unittest.TestCase):
    def test_unlisted_runtime_reference_keeps_release_and_the_closed_package_profile(self):
        with tempfile.TemporaryDirectory(prefix='parties-runtime-graph-') as temporary:
            root = Path(temporary)
            (root/'child').mkdir()
            (root/'global.json').write_bytes((ROOT/'global.json').read_bytes())
            child_source = root / "child/Example.cs"
            child_source.write_text("public class Example { public int Value => 1; }")
            (root/'child/Child.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
<Target Name="VerifyRuntimeProfile" BeforeTargets="Build">
<Error Condition="'$(Configuration)' != 'Release'" Text="Runtime configuration drifted" />
<Error Condition="'$(HexalithUsePublishedSdkForHostedTests)' != 'true'" Text="Runtime package boundary drifted" />
</Target></Project>''')
            (root/'Owner.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net11.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<Import Project="{ROOT}/scripts/PartiesRuntimeGraph.props" />
<ItemGroup><ProjectReference Include="child/Child.csproj" AdditionalProperties="$(_PartiesRuntimeBuildProperties)" /></ItemGroup>
</Project>''')
            (root/'Probe.slnx').write_text('<Solution><Project Path="Owner.csproj" /></Solution>')
            (root/'Directory.Solution.targets').write_text(f'<Project><Import Project="{ROOT}/Directory.Solution.targets" /></Project>')
            command = ['dotnet','build','Probe.slnx','--configuration','Release',
                                     '--disable-build-servers','-m:1','-noAutoResponse','-warnaserror',
                                     '-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true',
                                     '-p:ArtifactsPath='+str(root/'artifacts')]
            result = subprocess.run(command, cwd=root, env=os.environ.copy(), capture_output=True, text=True, timeout=60)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            outputs = [path for path in root.rglob('Child.dll') if 'bin' in path.parts]
            self.assertTrue(outputs, result.stdout)
            for output in outputs:
                self.assertIn('release', [part.lower() for part in output.parts])
            self.assertFalse(any('debug' in [part.lower() for part in path.parts] for path in root.rglob('Child.dll')))
            marker = json.loads((root/'artifacts/runtime-profile/Release/solution-build-completed.json').read_text())
            registered = next((path for path in outputs if str(path) in marker['projects']), None)
            self.assertIsNotNone(registered, marker)
            before = hashlib.sha256(registered.read_bytes()).hexdigest()
            timestamp = child_source.stat()
            child_source.write_text("public class Example { public int Value => 2; }")
            os.utime(child_source, ns=(timestamp.st_atime_ns, timestamp.st_mtime_ns))
            subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=60, check=True)
            self.assertNotEqual(before, hashlib.sha256(registered.read_bytes()).hexdigest())
            compiled = registered.stat().st_mtime_ns
            subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=60, check=True)
            self.assertEqual(compiled, registered.stat().st_mtime_ns)


if __name__ == '__main__':
    unittest.main()
