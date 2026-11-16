"""Verify the actual package profile survives SDK solution-reference traversal."""
from pathlib import Path
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
            (root/'child/Child.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
<Target Name="VerifyRuntimeProfile" BeforeTargets="Build">
<Error Condition="'$(Configuration)' != 'Release'" Text="Runtime configuration drifted" />
<Error Condition="'$(HexalithUsePublishedSdkForHostedTests)' != 'true'" Text="Runtime package boundary drifted" />
</Target></Project>''')
            (root/'Owner.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
<Import Project="{ROOT}/scripts/PartiesRuntimeGraph.props" />
<ItemGroup><ProjectReference Include="child/Child.csproj" AdditionalProperties="$(_PartiesRuntimeBuildProperties)" /></ItemGroup>
</Project>''')
            (root/'Probe.slnx').write_text('<Solution><Project Path="Owner.csproj" /></Solution>')
            (root/'Directory.Solution.targets').write_text(f'<Project><Import Project="{ROOT}/Directory.Solution.targets" /></Project>')
            result = subprocess.run(['dotnet','build','Probe.slnx','--configuration','Release',
                                     '--disable-build-servers','-m:1','-noAutoResponse','-warnaserror',
                                     '-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true',
                                     '-p:ArtifactsPath='+str(root/'artifacts')],
                                    cwd=root, env=os.environ.copy(), capture_output=True, text=True, timeout=60)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            outputs = [path for path in root.rglob('Child.dll') if 'bin' in path.parts]
            self.assertTrue(outputs, result.stdout)
            for output in outputs:
                self.assertIn('release', [part.lower() for part in output.parts])
            self.assertFalse(any('debug' in [part.lower() for part in path.parts] for path in root.rglob('Child.dll')))
            self.assertTrue((root/'artifacts/runtime-profile/Release/solution-build-completed.json').is_file())


if __name__ == '__main__':
    unittest.main()
