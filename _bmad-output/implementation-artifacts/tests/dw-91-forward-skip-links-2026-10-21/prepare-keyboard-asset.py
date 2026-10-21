"""Create an isolated diagnostic manifest that replaces only the keyboard asset."""
import json
import sys
from pathlib import Path

manifest, candidate_root, output = (Path(arg) for arg in sys.argv[1:])
assert output.resolve() != manifest.resolve(), "Keep the built manifest unchanged"
data = json.loads(manifest.read_text())
node = data["Root"]["Children"]["_content"]["Children"]["Hexalith.FrontComposer.Shell"]["Children"]["js"]["Children"]["fc-keyboard.js"]
assert node["Asset"]["SubPath"] == "js/fc-keyboard.js"
assert (candidate_root / "js/fc-keyboard.js").is_file()
node["Asset"]["ContentRootIndex"] = len(data["ContentRoots"])
data["ContentRoots"].append(str(candidate_root.resolve()) + "/")
output.write_text(json.dumps(data))
