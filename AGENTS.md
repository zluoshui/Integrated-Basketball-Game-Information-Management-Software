<!-- TRELLIS:START -->
# Trellis Instructions

These instructions are for AI assistants working in this project.

This project is managed by Trellis. The working knowledge you need lives under `.trellis/`:

- `.trellis/workflow.md` — development phases, when to create tasks, skill routing
- `.trellis/spec/` — package- and layer-scoped coding guidelines (read before writing code in a given layer)
- `.trellis/workspace/` — per-developer journals and session traces
- `.trellis/tasks/` — active and archived tasks (PRDs, research, jsonl context)

If a Trellis command is available on your platform (e.g. `/trellis:finish-work`, `/trellis:continue`), prefer it over manual steps. Not every platform exposes every command.

If you're using Codex or another agent-capable tool, additional project-scoped helpers may live in:
- `.agents/skills/` — reusable Trellis skills
- `.codex/agents/` — optional custom subagents

Managed by Trellis. Edits outside this block are preserved; edits inside may be overwritten by a future `trellis update`.

<!-- TRELLIS:END -->

## Project Knowledge Base Requirements

- Before any code, design, test, or documentation change, read `docs/project-knowledge/README.md` and the linked knowledge-base pages relevant to the touched area.
- If a change affects architecture, data schema, JSON/CSV contracts, database migrations, operator workflows, validation commands, known risks, or long-term decisions, update the corresponding file under `docs/project-knowledge/` in the same change.
- Do not treat the knowledge base as optional handoff notes. It is part of the project source of truth for future human developers and AI developers.
- Keep knowledge-base updates concise, factual, and synchronized with the implemented behavior.
