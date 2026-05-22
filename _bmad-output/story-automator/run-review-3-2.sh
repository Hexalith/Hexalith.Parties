#!/usr/bin/env bash
set -euo pipefail

cd /d/Hexalith.Parties
export PATH="/c/Users/JeromePiquot/.local/bin:/c/Users/JeromePiquot/AppData/Roaming/npm:/c/Program Files/nodejs:/c/Program Files/Git/cmd:/c/Program Files/Git/bin:/usr/bin:$PATH"
export USERPROFILE="C:/Users/JeromePiquot"
export PYTHONPATH="D:/Hexalith.Parties/.agents/skills/bmad-story-automator/src"
export PYTHONUTF8=1

unset CLAUDECODE
claude --dangerously-skip-permissions 'Execute the story-automator review workflow for story 3.2.

READ this skill first: .agents\skills\bmad-story-automator-review\SKILL.md
READ this workflow file next: .agents\skills\bmad-story-automator-review\workflow.yaml
Then read: .agents\skills\bmad-story-automator-review\instructions.xml
Validate with: .agents\skills\bmad-story-automator-review\checklist.md
Story file: _bmad-output/implementation-artifacts/3-2-provide-typed-parties-client-registration.md
Review implementation, find issues, fix them automatically. auto-fix all issues without prompting.

Do not review _bmad-output/implementation-artifacts/3-2-party-index-projection-handler-and-actor.md; that is an older unrelated artifact.'
