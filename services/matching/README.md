# Suolingo matching service

FastAPI backend for Suolingo. **Milestone 1 scope:** the hand-recording contract,
validation, and input-quality inspection. There is no sign evaluation yet (mock
evaluation arrives in Milestone 2; a real engine after research).

Setup and commands: see [docs/SETUP.md](../../docs/SETUP.md#backend-fastapi).

```bash
uv sync --all-packages                                    # run at repo root: Python 3.13 + locked deps into ./.venv
uv run pytest                                             # tests
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000    # dev server
uv run suolingo-recording inspect ../../tests/fixtures/recordings/synthetic-interruption.json
uv run suolingo-recording export-schema                   # regenerate contracts/hand-recording schema
```

Layout:

- `app/schemas/` — Pydantic models for the transport contracts (source of the JSON Schema in `contracts/`).
- `app/recordings/` — loading and input-quality analysis (tracking coverage, interruptions, frame timing).
- `app/api/` — HTTP routes (`GET /health`, `POST /v1/recordings/inspect`).
- `app/cli.py` — `suolingo-recording` command.

`matching/` and `content/` modules from the project plan are added when they have real content.
