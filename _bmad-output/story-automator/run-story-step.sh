#!/usr/bin/env bash
set -euo pipefail

step="${1:?step required}"
story="${2:?story required}"
agent="${3:?agent required}"
cycle="${4:-}"
state_file="D:/Hexalith.Parties/_bmad-output/story-automator/orchestration-1-20260521-062818.md"

cd /d/Hexalith.Parties
export PATH="/c/Users/JeromePiquot/.local/bin:/c/Users/JeromePiquot/AppData/Roaming/npm:/c/Program Files/nodejs:/c/Program Files/Git/cmd:/c/Program Files/Git/bin:/usr/bin:$PATH"
export USERPROFILE="C:/Users/JeromePiquot"
export PYTHONPATH="D:/Hexalith.Parties/.agents/skills/bmad-story-automator/src"
export PYTHONUTF8=1

if [ "$agent" = "codex" ]; then
  export AI_COMMAND="codex exec --full-auto"
else
  unset AI_COMMAND
fi

args=(tmux-wrapper build-cmd "$step" "$story" --agent "$agent" --state-file "$state_file")
if [ -n "$cycle" ]; then
  args+=("cycle=$cycle")
fi

cmd="$(/c/Python314/python.exe -m story_automator "${args[@]}")"
eval "$cmd"
