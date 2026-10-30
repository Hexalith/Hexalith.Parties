"""Check the independent source capability combinations without widening diagnostic scope."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET


class FocusedHarnessTests(unittest.TestCase):
 def test_independent_capabilities_preserve_original_lanes_and_source_scope(self):
  here=Path(__file__).resolve().parent
  root=here.parents[3]
  expected={'retained':set(),'shared':{'HFC_SHARED_RECONCILE_READINESS','HES_CANCELLABLE_PROJECTIONS'},
            'shared-legacy-transport':{'HFC_SHARED_RECONCILE_READINESS'},
            'retained-cancellable-transport':{'HES_CANCELLABLE_PROJECTIONS'}}
  with tempfile.TemporaryDirectory() as directory:
   subprocess.run([sys.executable,str(here/'create-focused-harness.py'),str(root),directory],check=True,capture_output=True,timeout=5)
   baseline=None
   self.assertEqual({p.name for p in Path(directory).iterdir()},set(expected))
   for name,capabilities in expected.items():
    project=ET.parse(Path(directory)/name/'Focused.csproj')
    self.assertEqual(set(project.findtext('PropertyGroup/DefineConstants').split(';')),{'HFC_SHARED_UI'}|capabilities)
    sources={node.attrib['Include'] for node in project.findall('ItemGroup/Compile')}
    self.assertTrue(all(Path(source).is_file() for source in sources))
    if baseline is None:
     baseline=sources
    self.assertEqual(sources,baseline)


if __name__=='__main__':
 unittest.main()
