import os, signal, sys, subprocess, json, hashlib, time
from pathlib import Path


def run(name, cwd, args, timeout=300):
 d=Path(__file__).resolve().parent
 t=time.monotonic()
 with (d/(name+'.txt')).open('wb') as out:
  p=subprocess.Popen(args,cwd=cwd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,start_new_session=True)
  try:
   raw,_=p.communicate(timeout=timeout)
   code=p.returncode
  except subprocess.TimeoutExpired:
   # Own ordinary descendants in this process group, not processes escaping it.
   def terminate_group(sig):
    try:
     os.killpg(p.pid,sig)
    except ProcessLookupError:
     pass
   terminate_group(signal.SIGTERM)
   try:
    p.communicate(timeout=1)
   except subprocess.TimeoutExpired:
    pass
   finally:
    terminate_group(signal.SIGKILL)
   raw,_=p.communicate()
   raw+=f'\nVERIFICATION TIMEOUT: {timeout:g} seconds\n'.encode()
   code=124
  out.write(raw)
 raw=(d/(name+'.txt')).read_bytes()
 (d/(name+'.json')).write_text(json.dumps(dict(command=args,cwd=cwd,exit_code=code,elapsed_seconds=round(time.monotonic()-t,2),log=name+'.txt',sha256=hashlib.sha256(raw).hexdigest()),indent=2)+'\n')
 print(json.dumps(dict(name=name,exit_code=code)))
 print(raw.decode(errors='replace')[-3000:])
 return code


if __name__=='__main__':
 name,cwd,*args=sys.argv[1:]
 sys.exit(run(name,cwd,args))
