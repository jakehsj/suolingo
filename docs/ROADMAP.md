# Suolingo Roadmap

Last updated: November 3, 2026.

## Status and planning assumptions

The confirmed deadline is the second week of December 2026. The exact date is open. The milestones below are a proposed delivery plan, not completed work or independently approved deadlines.

The user expects to implement most of the system, including the initial single-expression prototype. All four members have a Quest 3. Matching research proceeds separately, but its outputs must inform integration before the final weeks.

See [the project plan](PROJECT_PLAN.md) for confirmed decisions and open questions.

## Milestone 1 — Environment and observable hand input

Target: first week of November.

Status (November 3, 2026): **implemented; awaiting on-device verification.** The Unity project, packages, Quest settings, scene, status panel,
capture/export, FastAPI service, recording validation and quality CLI, and draft contract are in place. Unity EditMode tests,
backend tests, and a headless Android APK build pass on macOS. The acceptance criteria below still require
(1) one real Quest 3 recording inspected with `suolingo-recording inspect`, (2) the device checklist in
`contracts/hand-recording/README.md`, and (3) a second team member reproducing [SETUP.md](SETUP.md), including on Windows.

Deliverables:

- Create the Unity project using 6000.3.26f1 and select compatible Meta packages after verification.
- Establish Python/FastAPI development setup and the initial repository layout.
- Build and run MR passthrough with hand tracking on Quest 3.
- Add tracking-status display and a minimal optional hand-motion file export.
- Draft the data contract based on actual SDK observations.

Acceptance criteria:

- The documented setup can be reproduced by another team member; verify Windows/macOS paths when those environments are represented.
- At least one recorded attempt contains interpretable timestamps, hand observations, and tracking validity.
- A tracking interruption is observable rather than silently treated as valid stationary motion.

No visual replay editor is required at this stage.

## Milestone 2 — Complete one-expression loop with mock evaluation

Target: second week of November.

Deliverables:

- Expression selection, demonstration, explicit start, countdown, bounded capture, result, and retry.
- A FastAPI HTTP endpoint with clearly labeled mock evaluation.
- Configurable backend address and Quest-to-development-server connectivity.
- A tool that submits a saved attempt to the same evaluation path.
- Matching/content research brief: first candidate method, required data, validation approach, and known limitations.

Acceptance criteria:

- One attempt can travel from Quest through FastAPI and back to a result screen.
- Success, retry, unscorable input, and network failure can be exercised independently.
- The same saved input can be submitted repeatedly without wearing a headset.
- Mock output is not represented as validated sign-language evaluation.

Decision checkpoint: aim to select the first matching experiment by the end of this milestone. Do not wait for a perfect algorithm before testing one.

## Milestone 3 — First real evaluation and scope decision

Target: third week of November.

Deliverables:

- Integrate a real matching implementation behind the engine boundary.
- Obtain suitable reference and evaluation examples for a small expression set.
- Evaluate correct attempts, near misses, and tracking failures.
- Measure runtime and resource usage to inform hosting.

Acceptance criteria:

- Report observed results and limitations, including attempts from someone other than the reference creator.
- Distinguish incorrect performance from insufficient input quality.
- Record the supported expressions, evaluated components, and threshold-selection approach.

Decision checkpoint:

- Choose the feasible final expression set; no fixed count is promised yet.
- Decide what real-time feedback can be delivered, or explicitly renegotiate the original requirement.
- Choose shared hosting based on measured compute/network needs and budget.

## Milestone 4 — MVP feature completion

Target: fourth week of November.

Proposed deliverables:

- Expand only to validated expressions.
- Add quiz and local progress if retained in the final scope.
- Improve actionable feedback and error recovery.
- Implement the agreed real-time evaluation scope, if feasible and retained.
- Make the shared backend usable by the team.

Acceptance criteria:

- Team members can run the intended learning flow on their own devices.
- Server or tracking failure does not produce a false incorrect-sign result.
- The submission scope and any deviations from the original proposal are documented.

## Milestone 5 — Feature freeze and user testing

Target: first week of December.

Deliverables:

- Freeze major feature additions.
- Run cross-user and cross-device testing with reproducible issue reports.
- Adjust feedback and thresholds using documented evidence.
- Rehearse server startup/deployment and presentation networking.
- Test the configurable local fallback if retained.

Acceptance criteria:

- No known blocker in the primary demo loop.
- Changes to matching are checked against saved examples, including near misses and tracking failures.
- Demo latency and failure behavior are measured on the intended setup; numerical targets remain to be agreed.

## Milestone 6 — Presentation readiness

Target: second week of December; exact date TBD.

Deliverables:

- Final Quest build and reproducible backend version.
- Demo script, startup instructions, endpoint configuration, and recovery procedure.
- Presentation material explaining the supported scope and known limitations honestly.

Acceptance criteria:

- Rehearse the entire demonstration using the final build and intended network.
- Verify any fallback independently; do not assume a laptop server is automatically reachable.
- Clearly distinguish implemented real-time evaluation, post-attempt evaluation, and tracking-status feedback.

## Suggested work distribution — not assigned

- **User:** initial prototype, tracking/data contracts, backend, matching integration, and overall coordination.
- **Unity-experienced teammate:** learning panels, demonstration playback, and result UI using mock responses.
- **Another teammate:** content sources, usage conditions, expression candidates, and possible sign-language review contacts.
- **Another teammate:** repeatable device setup, recording collection, test cases, and reproduction reports.

The last two contributions are critical to evaluation quality, even without programming experience. Define a small deliverable and handoff format before assigning each task.

## Immediate next implementation slice

When implementation is requested:

1. Confirm the initial package/environment choices still open in the project plan.
2. Establish a Quest build with passthrough and measured hand input.
3. Add explicit-start/countdown capture and optional export.
4. Define the first data schema from observed outputs.
5. Connect a mock FastAPI evaluation to the minimal learning UI.

Do not block this slice on the final matching algorithm, and do not deploy infrastructure merely because a hosting candidate is listed in the plan.
