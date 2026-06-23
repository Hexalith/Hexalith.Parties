# Hexalith.Parties Claude Instructions

## Shared Hexalith LLM Instructions

Before starting any work in this repository, read and follow
[`Hexalith.AI.Tools/hexalith-llm-instructions.md`](./Hexalith.AI.Tools/hexalith-llm-instructions.md).

Before working on any module user interface or UX, also read and follow
[`Hexalith.AI.Tools/hexalith-ux-instructions.md`](./Hexalith.AI.Tools/hexalith-ux-instructions.md).

## Git Submodules

- Never initialize or update nested submodules recursively unless the user
  explicitly asks for nested submodules.
- For repositories with submodules, initialize/update only root-level submodules
  by default.
- Avoid `git submodule update --init --recursive` and similar recursive
  submodule commands unless nested submodule initialization is explicitly
  requested.
