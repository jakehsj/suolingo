# Suolingo Project Plan

Last updated: November 3, 2026.

## 1. Purpose and status

Build a Meta Quest 3 mixed reality application that helps beginners practice Korean Sign Language by observing a demonstration, performing an expression, and receiving feedback.

This document records the planning conversation. It distinguishes **confirmed decisions**, **working proposals**, and **open questions**. It is not evidence that any implementation already exists.

The original project proposal describes learning content, hand-tracking-based comparison and real-time feedback, and interactive quizzes. Real-time sign evaluation is still an unresolved delivery requirement; completing tracking-status indicators alone does not fulfill it.

## 2. Confirmed decisions and constraints

- Target hardware: Meta Quest 3. All four team members have a device.
- Experience: MR, showing learning content over the real environment.
- Client: Unity Editor **6000.3.26f1 (LTS)**.
- Hand tracking: Meta SDK. Specific packages and versions have not been selected or verified against the editor version.
- Backend: Python/FastAPI, developed within this project and running separately from Unity.
- Matching engine: currently a black box; research will be conducted separately. No algorithm is selected.
- Initial goal: **select an expression → watch a demonstration → perform it → receive an evaluation → retry**.
- Accepted initial interaction: a learning panel, an explicit practice-start action, a countdown, a bounded capture interval, and a result screen. Automatic movement-boundary detection is not required for the first prototype.
- The user expects to implement most of the system and will implement this initial prototype.
- Team: four people, including one Unity-experienced member and the user with backend experience. Other assignments are suggestions, not commitments.
- There is no established source of sign demonstrations, reference motion data, or sign-language review yet.
- Deadline: the second week of December 2026. Exact date is open.

## 3. Initial scope

### Accepted prototype

1. Select one expression.
2. Observe its demonstration.
3. Start practice and wait for a countdown.
4. Capture hand-motion data for a bounded interval.
5. Submit the attempt and display evaluation feedback.
6. Retry.

The first integrated version may use explicitly labeled mock evaluation results. It must not present mock scores as validated sign recognition.

### Working proposals for the subsequent MVP

- Expand from one expression to a small, validated set; counts such as 3 or 5–10 are planning suggestions, not targets already agreed upon.
- Add a quiz without a visible demonstration and local learning progress.
- Begin with video demonstrations; add 3D hand replay only when justified.
- Use explicit capture boundaries before attempting automatic start/end detection. Capture duration should be configurable and remains undecided.
- Keep room scanning, persistent spatial anchors, accounts, rankings, multiplayer, and sentence translation outside the initial scope.

## 4. Architecture proposal

Use one repository containing a Unity client, one FastAPI service, shared data contracts, and development tools. Separate runtime deployment does not require separate repositories. Monorepo organization is a recommendation pending implementation, not an already-created structure.

### Unity responsibilities

- MR passthrough, hand visualization, and user interaction.
- Demonstration playback and learning-state management.
- Hand-motion capture with timestamps and tracking validity.
- Request submission, timeout/error handling, and feedback display.
- Optional local learning progress.

Suggested boundaries:

- `Tracking`: an `IHandTrackingSource` abstraction with Meta input and recorded-input implementations.
- `Learning`: expression selection, demonstration, countdown, capture, evaluation, result, and retry states.
- `Presentation`: panels, demonstrations, and feedback.
- `Networking`: an `IEvaluationClient` abstraction with mock and HTTP implementations.

Keep Meta-specific types at the tracking boundary. Evaluation should use measured tracking data rather than hand poses modified for visual interactions. Meta documents that `SyntheticHand` can alter joint data for grabbing and poking; tracking validity must also survive any filtering or last-valid-pose behavior.

### Backend responsibilities

- Validate incoming data and supported schema versions.
- Load expression metadata and reference data when the chosen algorithm requires it.
- Run preprocessing and matching.
- Return structured evaluation results and version information.

Keep `MatchingEngine.evaluate(...)` independent of FastAPI so the same engine can evaluate saved attempts from a command-line tool or tests.

Suggested internal modules: `api`, `schemas`, `matching`, and `content`. Initially, prefer files for content/reference metadata and avoid adding a database, queue, or separate inference service without a concrete requirement.

### Communication proposal

Start with an HTTP request after one completed attempt. Proposed endpoint: `POST /v1/evaluations`.

Proposed request contents:

- Expression ID and schema version.
- Timestamped left/right hand observations.
- Joint positions/rotations, coordinate reference information, and tracking validity as supported by the selected SDK.
- Capture metadata needed to interpret the data.

Proposed response contents:

- Evaluation status: pass, retry, or unscorable.
- Component scores where supported by the engine.
- Feedback codes that Unity maps to user-facing text.
- Reference-data and algorithm versions.

These are conceptual fields, not a finalized wire schema. Verify SDK outputs before specifying joint order, units, coordinate conventions, quaternion representation, sampling, missing-data behavior, and payload limits.

Network failures and evaluation failures must remain distinct. Do not label a connection failure or insufficient tracking as an incorrect sign. Treat initial numerical scores as internal similarity measures, not validated accuracy percentages.

Streaming transport, intermediate scoring, and acceptable latency remain open. Preserve the original real-time feedback requirement until it is implemented or explicitly renegotiated.

## 5. Motion recording: purpose and limits

“Recording” means saving timestamped hand-joint data and tracking status, not recording camera video.

Its initial purpose is development: reproduce a failed attempt, compare algorithm versions using identical input, and develop the backend without repeatedly wearing the headset. If template matching is selected, separately reviewed recordings may also become reference data.

Normal practice can buffer an attempt in memory, submit it, and discard it afterward. Permanent storage of every user's attempt is not required.

The minimum development tool is an optional export of an attempt and a backend tool that evaluates the saved file. A visual 3D replay interface is optional and can be deferred. File/schema/SDK versions should be recorded so examples remain interpretable.

Use participant IDs rather than names in development datasets. Keep reference-building data separate from evaluation data, and test with participants who did not create the references. Bulk recordings should not automatically enter Git; storage and retention details remain open.

## 6. Matching and content research

The initial working interpretation is **evaluation against the expression selected by the lesson**, rather than unconstrained recognition of any sign. Confirm this distinction if research changes the task.

Research must determine:

- Which expressions can be assessed reliably with available tracking signals.
- How demonstrations and reference motions will be obtained and reviewed.
- Which meaningful aspects cannot be assessed, including relevant facial/body cues.
- How to handle hand size, user position, handedness, movement speed, occlusion, and tracking loss.
- How to preserve relative hand positions and body-relative placement while normalizing hand shape.
- What correct, near-miss, and unscorable examples look like.
- How evaluation quality will be measured and thresholds selected.

Joint-angle comparison and DTW are candidate baselines only. Machine learning, GPU use, model training, and dataset availability are not assumed.

A demonstration video is not automatically a 3D reference-motion dataset. Temporary arbitrary gestures can validate the technical pipeline but must not be presented as reviewed Korean Sign Language learning content.

## 7. Proposed repository layout

Only the documentation currently exists; the following is a target layout to discuss during implementation.

```text
suolingo/
├── apps/unity/
│   ├── Assets/Suolingo/
│   │   ├── Core/
│   │   ├── Tracking/
│   │   ├── Learning/
│   │   ├── Presentation/
│   │   ├── Networking/
│   │   └── Scenes/
│   ├── Packages/
│   └── ProjectSettings/
├── services/matching/
│   ├── app/
│   │   ├── api/
│   │   ├── schemas/
│   │   ├── matching/
│   │   └── content/
│   └── tests/
├── contracts/
├── content/
├── tools/
├── tests/fixtures/
└── docs/
```

`contracts/` owns transport/data schemas and small examples. Top-level `content/` owns expression/reference metadata; backend `app/content/` contains loading code. Avoid duplicating authoritative content between these directories.

## 8. Cross-platform development proposal

- Run Unity natively on Windows or macOS; WSL is not required for Unity.
- Use the exact same editor and package versions. Install Android Build Support and the appropriate SDK/NDK/OpenJDK through Unity Hub.
- Run FastAPI in a native Python environment initially. Select and lock a Python version and dependencies during setup.
- WSL or Docker is optional for backend development; neither is an initial team prerequisite.
- Share assets together with `.meta` files, package manifests/locks, and project settings. Exclude generated caches such as `Library` and build outputs.
- Use relative paths, consistent filename case, and portable commands.
- Consider Git LFS for large models/videos; bulk datasets need a separate storage decision.
- Separate scene/prefab ownership to reduce merge conflicts.
- Windows developers can use supported Meta Horizon Link workflows. Native Quest Android builds remain the shared acceptance environment on both operating systems.

## 9. Hosting: recommendation, not a decision

No provider, budget, region, account, or deployment has been selected or authorized by this plan.

### Initial development recommendation

Run FastAPI on the developer's computer and connect the Quest over the same reachable LAN. Make the API base URL configurable. `localhost` on a Quest refers to the headset, not the development computer. Bind the development server to an appropriate network interface and verify firewall and Wi-Fi client-isolation behavior. If temporary HTTP is used, verify Android/Unity development settings; use HTTPS for a shared hosted endpoint.

### Shared testing recommendation

Once the first real HTTP evaluation works, deploy a single containerized service to a managed host. A managed container platform such as Cloud Run is a candidate for a short, stateless HTTP workload; an always-on managed service or small VM is another candidate if streaming or persistent loaded models become important.

Choose after measuring CPU, memory, model size, evaluation time, and whether GPU or long-lived connections are needed. Do not buy infrastructure based on an assumed matching implementation.

### Presentation recommendation

Use a rehearsed hosted endpoint as the primary service if venue networking supports it, with a tested local-server configuration as a fallback. Switching endpoints must not require code edits. A local fallback still requires reachable networking between headset and computer; test the actual arrangement.

Avoid relying on an untested free service's cold start for the presentation. Render currently documents idle spin-down for free web services. Cloud Run supports WebSockets, but connections are subject to request timeouts and need reconnection handling. Neither hosting model removes the need to measure latency and test failure behavior.

Hosting recommendations are provisional as of November 3, 2026. Verify provider behavior and pricing when choosing a plan.

## 10. Open decisions

- Exact demo date and success criteria.
- Meta SDK packages/versions and compatibility verification.
- Unity UI framework and rendering/package setup.
- Python version and dependency tooling.
- Demonstration source, usage permissions, and sign-language review process.
- Target expressions and their number.
- Matching algorithm, dataset, thresholds, compute needs, and quality metrics.
- Capture duration, sampling rate, coordinate normalization, and final data schema.
- Video versus 3D demonstration scope after the first prototype.
- Real-time evaluation scope, transport, and latency target.
- Local progress and quiz scope for the final submission.
- Hosting provider, region, budget, shared-endpoint access controls, and recording storage.
- Team assignments and whether the proposed repository structure is adopted unchanged.

## 11. Guidance for implementing agents

- Read this plan and the roadmap before acting; inspect the actual repository state rather than assuming scaffolding exists.
- The current request was documentation and planning, not implementation or deployment.
- Respect the fixed Unity editor version. Do not select SDK versions or algorithms silently.
- Keep mocked evaluation visibly distinct from a real, validated engine.
- Prioritize the single-expression loop and reproducible motion input before expanding features.
- Record new decisions and their rationale here; leave unresolved choices explicitly open.

## References

- Original project proposal PDF in the repository; avoid copying its personal identifiers into public documentation.
- [Meta input data overview](https://developers.meta.com/vr/documentation/unity/unity-isdk-input-processing/).
- [Meta development hardware setup](https://developers.meta.com/vr/documentation/unity/unity-env-device-setup/?locale=en_GB).
- [Meta passthrough setup](https://developers.meta.com/vr/documentation/unity/unity-passthrough-gs/).
- [Render free-service behavior](https://render.com/docs/free).
- [Cloud Run WebSockets](https://docs.cloud.google.com/run/docs/triggering/websockets).
