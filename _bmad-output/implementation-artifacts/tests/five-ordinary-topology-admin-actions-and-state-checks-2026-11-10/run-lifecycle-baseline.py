"""Replay new per-site controls against the approved baseline without modifying working files. Run from the repository root."""
import subprocess,sys,unittest
sys.path.insert(0,'tests/ci-lifecycle')
import test_qualification_controller as controller
import test_launch_recovery as launch
original=subprocess.check_output(['git','show','ef298d60d21e0a7113672276ea2c0c7e5f512cc3:scripts/qualify-disposable-ci.py'],text=True)
for module in (controller,launch):
 exec(compile(original,str(module.SCRIPT),'exec'),module.Q.__dict__)
suite=unittest.TestSuite()
suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(controller.ControllerSafetyTests))
for name in unittest.defaultTestLoader.getTestCaseNames(controller.MetadataDecisionTests):
 if name!='test_optional_records_admit_only_absence_or_a_regular_json_object':
  suite.addTest(controller.MetadataDecisionTests(name))
suite.addTest(launch.LaunchRecoveryTests('test_launch_recovery_refuses_a_dangling_record_instead_of_polling_for_absence'))
suite.addTest(launch.LaunchRecoveryTests('test_launch_recovery_validates_the_record_bytes_from_its_held_descriptor'))
result=unittest.TextTestRunner(verbosity=2).run(suite)
raise SystemExit(not result.wasSuccessful())
