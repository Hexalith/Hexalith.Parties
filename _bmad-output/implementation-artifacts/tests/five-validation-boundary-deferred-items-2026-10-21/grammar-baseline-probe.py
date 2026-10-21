from pathlib import Path
import json
import re
import subprocess

BASELINE = "63cd1279a82df47af3d16dd0ff9f002f3e4c86eb"
workflow = ".github/workflows/release.yml"
sources = {
    "baseline": subprocess.check_output(["git", "show", f"{BASELINE}:{workflow}"], text=True),
    "candidate": Path(workflow).read_text(),
}
pattern = r'if ! above_floor="\$\(jq -er --arg floor "\$floor_version" \'(.*?)\' "\$published_response"\)"; then'
results = []
invalid = ["3.16.0-a..b", "3.16.0-.", "3.16.0-.a", "3.16.0-a.", "3.16.0-a...b"]
for name, text in sources.items():
    expression = re.search(pattern, text, re.S)[1]
    for version in invalid + ["99.0.0-a-b.9", "99.0.0--"]:
        result = subprocess.run(["jq", "-er", "--arg", "floor", "3.15.1", expression],
                                input=json.dumps({"versions": [version]}), text=True, capture_output=True, check=False)
        expected = 5 if name == "candidate" and version in invalid else 0
        assert result.returncode == expected, (name, version, result)
        results.append({"source": name, "version": version, "exit_code": result.returncode,
                        "output": result.stdout.strip(), "error": result.stderr.strip()})
print(json.dumps({"baseline_revision": BASELINE, "results": results}, indent=2))
