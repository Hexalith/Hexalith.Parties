import importlib.util,json,os,signal,subprocess,sys,tempfile,time,unittest
from unittest import mock
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=Path.cwd()
spec=importlib.util.spec_from_file_location('queue_driver',HERE/'driver.py')
driver=importlib.util.module_from_spec(spec);spec.loader.exec_module(driver)

def alive(pid,birth):
    try:
        raw=Path('/proc',str(pid),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
        return fields[0] not in ('Z','X') and fields[19]==str(birth)
    except (FileNotFoundError,ProcessLookupError):return False

SCRIPT="""import json,os,pathlib,subprocess,sys,time
child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)'],start_new_session=True)
raw=pathlib.Path('/proc',str(child.pid),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
pathlib.Path(sys.argv[1]).write_text(json.dumps({'Pid':child.pid,'Start':fields[19]}))
if sys.argv[2]=='exit':os._exit(0)
time.sleep(120)
"""

class DriverControls(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=os.environ['TMPDIR']);self.scratch=Path(self.temp.name)
        self.foreign=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)'])
    def tearDown(self):
        self.foreign.kill();self.foreign.wait(timeout=5);self.temp.cleanup()
    def command(self,mode,timeout,admit=False):
        identity=self.scratch/'child.json'
        admission={'Observed':False}
        real_popen=subprocess.Popen
        def launch_after_admission(*args,**kwargs):
            child=real_popen(*args,**kwargs)
            deadline=time.monotonic()+5
            # Always return the actual Process, including failed admission. The
            # unchanged run_job then owns timeout/stop/reaping on every path.
            while time.monotonic()<deadline:
                try:
                    owned=json.loads(identity.read_text())
                    if alive(owned['Pid'],owned['Start']):
                        admission.update({'Observed':True,'Pid':owned['Pid'],'Start':owned['Start']})
                        break
                except (FileNotFoundError,ValueError):pass
                time.sleep(.005)
            return child
        if admit:
            with mock.patch.object(driver.subprocess,'Popen',side_effect=launch_after_admission):
                outcome=driver.run_job([sys.executable,'-c',SCRIPT,str(identity),mode],os.environ,self.scratch/'job.log',timeout,self.scratch,ROOT)
            self.assertTrue(admission['Observed'],'The detached fixture child was not admitted before the finite timeout test; exclusive cleanup was still awaited.')
        else:
            outcome=driver.run_job([sys.executable,'-c',SCRIPT,str(identity),mode],os.environ,self.scratch/'job.log',timeout,self.scratch,ROOT)
        owned=json.loads(identity.read_text())
        self.assertFalse(alive(owned['Pid'],owned['Start']));self.assertIsNone(self.foreign.poll())
        self.assertTrue(outcome['DescendantsReaped'])
        if admit:
            (HERE/'timeout-admission-control.json').write_text(json.dumps({'AdmissionObservedBeforeTimeoutWait':admission['Observed'],'AdmissionBudgetSeconds':5,'ActualWaitTimeoutSeconds':timeout,'CapturedChild':owned,'ChildExitedBeforeReturn':not alive(owned['Pid'],owned['Start']),'ForeignSurvived':self.foreign.poll() is None,'ActualOutcome':outcome,'ProductionDriverUnchanged':True},indent=2)+'\n')
        return outcome
    def test_original_parent_exits_with_detached_descendant(self):
        self.assertEqual(self.command('exit',10)['ExitCode'],0)
    def test_timeout_reaps_detached_descendant_before_return(self):
        self.assertEqual(self.command('sleep',.3,admit=True)['ExitCode'],255)
    def test_caller_disappearance_still_reaps_detached_descendant(self):
        identity=self.scratch/'child.json';log=self.scratch/'caller.log'
        code="""import importlib.util,os,pathlib,sys,threading,time
spec=importlib.util.spec_from_file_location('driver',sys.argv[1]);driver=importlib.util.module_from_spec(spec);spec.loader.exec_module(driver)
scratch=pathlib.Path(sys.argv[2]);root=pathlib.Path(sys.argv[3])
def disappear():
    while not (scratch/'child.json').exists():time.sleep(.005)
    os._exit(17)
threading.Thread(target=disappear,daemon=True).start()
driver.run_job([sys.executable,'-c',sys.argv[4],str(scratch/'child.json'),'sleep'],os.environ,scratch/'caller.log',10,scratch,root)
"""
        caller=subprocess.Popen([sys.executable,'-c',code,str(HERE/'driver.py'),str(self.scratch),str(ROOT),SCRIPT])
        self.assertEqual(caller.wait(timeout=10),17)
        owned=json.loads(identity.read_text());supervisor=json.loads(log.with_suffix('.supervisor.json').read_text())
        end=time.monotonic()+7
        while alive(supervisor['Pid'],supervisor['Start']) and time.monotonic()<end:time.sleep(.01)
        self.assertFalse(alive(supervisor['Pid'],supervisor['Start']));self.assertFalse(alive(owned['Pid'],owned['Start']))
        self.assertIsNone(self.foreign.poll())
    def test_active_command_reaps_adopted_parent_before_its_monitor_can_exit(self):
        scripts={
            'monitor.py':"""import os,pathlib,sys,time
root=pathlib.Path(sys.argv[1]);pid=int(sys.argv[2])
while pathlib.Path('/proc',str(pid)).exists():time.sleep(.005)
(root/'monitor-done').touch()
""",
            'orphan.py':"""import json,os,pathlib,subprocess,sys,time
root=pathlib.Path(sys.argv[1]);original=os.getppid()
monitor=subprocess.Popen([sys.executable,str(root/'monitor.py'),str(root),str(os.getpid())])
(root/'orphan-ready').touch()
while os.getppid()==original:time.sleep(.005)
raw=pathlib.Path('/proc',str(os.getpid()),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
(root/'adopted.json').write_text(json.dumps({'Pid':os.getpid(),'Start':fields[19],'Parent':os.getppid(),'MonitorPid':monitor.pid}))
while not (root/'exit').exists():time.sleep(.005)
os._exit(0)
""",
            'intermediate.py':"""import os,pathlib,subprocess,sys,time
root=pathlib.Path(sys.argv[1]);subprocess.Popen([sys.executable,str(root/'orphan.py'),str(root)])
while not (root/'orphan-ready').exists():time.sleep(.005)
os._exit(0)
""",
            'active.py':"""import json,pathlib,subprocess,sys,time
root=pathlib.Path(sys.argv[1]);child=subprocess.Popen([sys.executable,str(root/'intermediate.py'),str(root)]);child.wait()
deadline=time.monotonic()+3
while not (root/'adopted.json').exists() and time.monotonic()<deadline:time.sleep(.005)
if not (root/'adopted.json').exists():sys.exit(24)
(root/'exit').touch()
while not (root/'monitor-done').exists() and time.monotonic()<deadline:time.sleep(.005)
if not (root/'monitor-done').exists():
    value=json.loads((root/'adopted.json').read_text());raw=pathlib.Path('/proc',str(value['Pid']),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
    (root/'blocked.json').write_text(json.dumps({'AdoptedParentState':fields[0],'AdoptedParentBirth':fields[19],'Parent':int(fields[1]),'MonitorStillWaiting':True}))
    sys.exit(23)
"""}
        for name,source in scripts.items():(self.scratch/name).write_text(source)
        outcome=driver.run_job([sys.executable,str(self.scratch/'active.py'),str(self.scratch)],os.environ,self.scratch/'active.log',10,self.scratch,ROOT)
        blocked=self.scratch/'blocked.json'
        self.assertEqual(outcome['ExitCode'],0,blocked.read_text() if blocked.exists() else outcome)
        self.assertTrue((self.scratch/'monitor-done').exists());self.assertIsNone(self.foreign.poll())
    def test_containment_failure_blocks_later_job_and_scratch_release(self):
        private=self.scratch/'private';private.mkdir();(private/'queue').mkdir()
        (private/'driver.py').write_bytes((HERE/'driver.py').read_bytes())
        failure="import os,pathlib; pathlib.Path(os.environ['PARTIES_JOB_SUPERVISOR_STATE'],'failure').write_text('controlled containment failure')"
        (private/'queue/01.json').write_text(json.dumps({'Args':[sys.executable,'-c',failure]}))
        later=self.scratch/'later'
        (private/'queue/02.json').write_text(json.dumps({'Args':[sys.executable,'-c',"import pathlib,sys; pathlib.Path(sys.argv[1]).touch()",str(later)]}))
        environment=os.environ.copy();environment['TMPDIR']=str(self.scratch)
        with (self.scratch/'blocked.log').open('w') as stream:
            caller=subprocess.Popen([sys.executable,str(private/'driver.py')],env=environment,stdout=stream,stderr=subprocess.STDOUT)
            try:
                end=time.monotonic()+10
                while not (private/'containment-failure.json').exists() and time.monotonic()<end:time.sleep(.01)
                receipt=json.loads((private/'containment-failure.json').read_text())
                self.assertTrue(receipt['LaterJobsRefused']);self.assertTrue(receipt['ScratchReleaseRefused'])
                (private/'finish').touch();time.sleep(.05)
                self.assertIsNone(caller.poll());self.assertTrue(self.scratch.is_dir());self.assertFalse(later.exists())
            finally:
                # The injected failure created no descendants. This control owns and reaps only its blocked queue caller.
                caller.kill();caller.wait(timeout=5)

if __name__=='__main__':unittest.main()
