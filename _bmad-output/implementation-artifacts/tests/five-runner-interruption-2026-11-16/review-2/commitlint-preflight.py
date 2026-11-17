import hashlib,json,os,re,sys
from pathlib import Path
from driver import run_job
private=Path(__file__).resolve().parent;root=Path.cwd();scratch=Path(os.environ['TMPDIR'])
source=private.parent/'parent-commit-message.txt';raw=source.read_bytes();candidate=scratch/'candidate.txt';candidate.write_bytes(raw)
tool_root=root/'references/Hexalith.Builds'
locked=json.loads((tool_root/'package-lock.json').read_text())['packages']['node_modules/@commitlint/cli']['version']
installed=json.loads((tool_root/'node_modules/@commitlint/cli/package.json').read_text())['version']
assert locked==installed=='21.2.4'
args=['node',str(tool_root/'node_modules/@commitlint/cli/cli.js'),'--config',str(root/'commitlint.config.mjs'),'--edit',str(candidate)]
outcome=run_job(args,os.environ,private/'commitlint-preflight.log',30,scratch,root)
if outcome['ExitCode']!=0:sys.exit(outcome['ExitCode'])
header=raw.decode().splitlines()[0]
match=re.fullmatch(r'(feat|fix|perf|docs|refactor|test|revert|build|ci|style)(?:\([^()]+\))?!?: (.+)',header)
assert match and match.group(2)[0].islower() and not match.group(2).endswith('.'),'The stricter Hexalith header policy rejected the parent-owned candidate'
(private/'validated-parent-candidate.txt').write_bytes(raw)
value={'Scope':'Pinned CLI preflight on the existing parent-owned exact candidate; publication and final message validation remain the parent handoff','CliVersion':installed,'PinnedLockSha256':hashlib.sha256((tool_root/'package-lock.json').read_bytes()).hexdigest(),'PartiesConfigSha256':hashlib.sha256((root/'commitlint.config.mjs').read_bytes()).hexdigest(),'PinAuthority':'Root-declared Builds package-lock.json at73e60af8a1fcdf3f06726d5d1b6e1199cce778ee; current Parties config','RootNpmBootstrapAvailable':(root/'package.json').exists(),'CandidateSha256':hashlib.sha256(raw).hexdigest(),'Command':args,'ExitCode':0,'HexalithHeaderPolicyPassed':True,'ParentCandidateUnchangedDuringValidation':source.read_bytes()==raw,'DescendantsReaped':outcome['DescendantsReaped']}
(private/'commitlint-preflight.json').write_text(json.dumps(value,indent=2));print(json.dumps({'PinnedCommitlint':value['CliVersion'],'Passed':True}))
