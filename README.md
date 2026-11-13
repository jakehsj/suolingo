# Suolingo

A Meta Quest 3 mixed reality app for introductory Korean Sign Language learning.

The initial learning loop is: **select an expression → watch a demonstration → perform it → receive an evaluation → retry**.

## Project documents

- [Project plan](docs/PROJECT_PLAN.md): confirmed decisions, proposed architecture, implementation boundaries, and open questions.
- [Roadmap](docs/ROADMAP.md): proposed milestones, acceptance criteria, and possible team contributions.
- [Setup](docs/SETUP.md): reproducible development environment, build, recording retrieval, and backend commands.

Read both documents before implementing a feature. A proposal or open question is not an approved technical decision. Update the documents when decisions are made; do not silently turn assumptions into requirements.

## Current status

Milestone 1 (environment and observable hand input) was implemented on November 3, 2026, and has **not yet been verified on a headset**.

- `apps/unity`: Quest 3 MR passthrough app with Meta hand tracking, a tracking-status panel, and optional JSON export of hand motion.
- `services/matching`: FastAPI service that validates recordings and reports input quality. Sign evaluation is not yet available, including mock evaluation.
- `contracts/hand-recording`: draft recording contract 0.1.0.

No matching algorithm or hosting provider has been selected. To set up and run the project, see [docs/SETUP.md](docs/SETUP.md).

Confirmed technologies:

- Meta Quest 3, with MR passthrough and Meta SDK hand tracking.
- Unity Editor **6000.3.26f1 (LTS)**.
- Python and FastAPI for the separately running matching backend.

The deadline is the **second week of December 2026**; the exact presentation date remains unspecified.
