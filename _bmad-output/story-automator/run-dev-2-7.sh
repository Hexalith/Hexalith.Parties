#!/usr/bin/env bash
set -euo pipefail

cd /d/Hexalith.Parties
export PATH="/c/Users/JeromePiquot/AppData/Roaming/npm:/c/Program Files/nodejs:/c/Program Files/Git/cmd:/c/Program Files/Git/bin:/usr/bin:$PATH"
export USERPROFILE="C:/Users/JeromePiquot"
export PYTHONPATH="D:/Hexalith.Parties/.agents/skills/bmad-story-automator/src"
export PYTHONUTF8=1
export AI_COMMAND="codex exec --full-auto"

cmd="$(/c/Python314/python.exe -m story_automator tmux-wrapper build-cmd dev 2.7 --agent codex --state-file D:/Hexalith.Parties/_bmad-output/story-automator/orchestration-1-20260521-062818.md)"
eval "$cmd"
