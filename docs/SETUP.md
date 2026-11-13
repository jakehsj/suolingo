# Suolingo development setup

Last updated: November 3, 2026 (Milestone 1).

This guide reproduces the Milestone 1 environment: a Quest 3 MR passthrough build with hand tracking,
a tracking-status panel, optional hand-motion export, and the FastAPI backend that validates recordings.

> Verified on macOS (Apple silicon), including a headless project setup, EditMode tests, and Android build.
> The Windows steps follow the same tools but have **not yet been verified** by a team member. Report the
> first successful Windows run in this file.

## 1. Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| Unity Hub + Unity Editor | **6000.3.26f1** exactly | Add modules: *Android Build Support*, *Android SDK & NDK Tools*, *OpenJDK*. |
| Meta Quest 3 | Developer mode enabled | Use the Meta Horizon mobile app: Devices → Developer Mode. |
| uv | ≥ 0.11 | Python package/environment manager; installs Python 3.13 for the backend (workspace at repo root). |
| Git | any recent | |

Install uv:

- macOS: `brew install uv`
- Windows (PowerShell): `powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"`

`adb` is included with Unity's Android SDK, so a separate installation is not required:

- macOS: `/Applications/Unity/Hub/Editor/6000.3.26f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`
- Windows: `C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`

## 2. Repository layout

```text
apps/unity/            Unity client (URP, Meta XR Core SDK, OpenXR)
services/matching/     FastAPI backend (Python 3.13, uv)
contracts/             Data contracts shared by client and backend
tests/fixtures/        Small shared fixtures (synthetic recordings)
docs/                  Plan, roadmap, setup
```

## 3. Unity client

### Packages (pinned in `apps/unity/Packages/manifest.json`)

| Package | Version | Why |
| --- | --- | --- |
| `com.meta.xr.sdk.core` | 207.0.1 | Meta XR Core SDK: OVRCameraRig, passthrough, hand tracking (OVRPlugin). Requires Unity ≥ 6000.0.66f2. From the scoped registry `https://npm.developer.oculus.com`. |
| `com.unity.xr.openxr` | 1.16.1 | XR provider used by the Meta SDK (Meta XR Feature). Version bundled with and recommended by Unity 6000.3. |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP from the Universal 3D template. |

The packages resolve automatically when the project opens. The first import downloads Meta's packages and can take several minutes.

### Open and configure

1. Unity Hub → *Add project from disk* → `apps/unity` → open with **6000.3.26f1**.
2. Run **Suolingo → Setup → Apply Quest Settings + Rebuild M1 Scene**. The command:
   - Switches to Android and sets IL2CPP/ARM64, Vulkan, Linear color, min API 32, and the app ID `dev.suolingo.quest`.
   - Enables the OpenXR loader with the *Meta XR* feature group and the *Meta Quest Support* feature.
   - Sets the Meta project configuration: Quest 3/3S, controllers + hands, **high** hand-tracking frequency,
     required passthrough, and the OpenXR hand skeleton (26 joints).
   - Applies the automatic fixes from Meta's Project Setup Tool.
   - Regenerates `Assets/Suolingo/Scenes/HandTrackingCheck.unity`.
3. Optional check: **Meta → Tools → Project Setup Tool** should show no outstanding *Required* items for Android.

The settings and scene are already committed. Rerun the command only after a package update or when settings drift.

Headless equivalent with the [Unity CLI](https://unity.com):

```bash
unity run apps/unity --editor-version 6000.3.26f1 -- \
  -executeMethod Suolingo.Editor.SuolingoProjectSetup.ApplyAllBatch -logFile -
```

### Build and install on Quest 3

1. Connect the headset over USB and accept the *Allow USB debugging* prompt in the headset.
2. `adb devices` should list the headset.
3. Build:
   - Editor: **Suolingo → Build → Build Quest APK (Development)** → `apps/unity/Builds/Android/suolingo.apk`.
   - Or: *File → Build Profiles → Android → Build And Run*.
   - Headless: `unity run apps/unity --editor-version 6000.3.26f1 -- -executeMethod Suolingo.Editor.SuolingoBuild.BuildQuestApkBatch -logFile -`
4. Install: `adb install -r apps/unity/Builds/Android/suolingo.apk`. The app appears under *Library → Unknown sources*.

### Using the Milestone 1 scene

- Passthrough shows the room; your tracked hands are drawn with Meta's hand meshes.
- A status panel appears about 0.7 m in front of you and shows each hand as **TRACKED (high/low confidence)** or
  **LOST** (with elapsed time), plus the per-hand interruption count for the session.
- **Record an attempt:** hold an index–thumb pinch on either hand for 1 s. A 3 s countdown follows, then a 4 s capture.
  Pinch during the countdown to cancel. `Space` toggles recording when running through Link in the Editor.
  This pinch trigger is temporary until the learning panel provides a start button.
- After capture, the panel shows the number of frames, the tracked percentage, and gaps per hand, plus the filename.

### Retrieve recordings

Recordings are saved on the headset in `/sdcard/Android/data/dev.suolingo.quest/files/recordings/`:

```bash
mkdir -p recordings
adb pull /sdcard/Android/data/dev.suolingo.quest/files/recordings/ recordings/
```

`recordings/` is git-ignored. Do not commit bulk recordings (see the project plan). Use pseudonymous
participant IDs, set on the `HandMotionRecorder` component.

### Unity tests

```bash
unity test apps/unity --editor-version 6000.3.26f1 --mode EditMode --output editmode-results.xml
```

Alternatively, use *Window → General → Test Runner → EditMode*.

## 4. Backend (FastAPI)

Python projects form one **uv workspace** rooted at the repository: the root `pyproject.toml` lists `services/*` as members,
and they share a single `uv.lock` and a single `.venv` at the repository root. Editors that open the repository root
(VS Code, PyCharm) find that interpreter automatically, so adding a new service does not require switching interpreters.

```bash
uv sync --all-packages      # repo root: installs Python 3.13 (if needed) and locked deps into ./.venv
cd services/matching
uv run pytest               # tests (uv uses the workspace .venv from any member directory)
uv run uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

`uv sync` creates and fills `./.venv` but does **not** activate it in your shell. There are two ways to use it:

1. **No activation (recommended):** prefix commands with `uv run`, as above. uv finds the workspace `.venv` from any
   directory inside the repository and re-syncs it if the lockfile changed.
2. **Activate the environment** when you want plain `python`, `pytest`, `uvicorn`, or `suolingo-recording` commands:

   | Shell | Activate (from the repository root) |
   | --- | --- |
   | macOS / Linux (zsh, bash) | `source .venv/bin/activate` |
   | Windows PowerShell | `.venv\Scripts\Activate.ps1` |
   | Windows cmd | `.venv\Scripts\activate.bat` |

   Run `deactivate` to leave the environment. If PowerShell blocks the script, run
   `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` once, or use `uv run` instead.
   Check which interpreter is active with `python -c "import sys; print(sys.prefix)"`, which should print `<repo>/.venv`.

   If another environment manager also changes `python`, such as pyenv shims or conda `base`, activating `.venv` still
   takes precedence in that shell. `uv run` does not depend on it.

VS Code: if the interpreter isn't picked up, run *Python: Select Interpreter* once and choose `./.venv`. New integrated
terminals then activate it automatically.
To add a new Python service, create `services/<name>/pyproject.toml` and run `uv sync --all-packages`.

The `uv` commands are the same on Windows (PowerShell). Open `http://localhost:8000/docs` for the API explorer.

Milestone 1 endpoints:

- `GET /health` — service, version, and contract version. Reports `"evaluation": "not-implemented"`.
- `POST /v1/recordings/inspect` — validates a recording and returns an **input-quality report**
  (tracking coverage, interruptions, frame timing). This report is not a sign evaluation.

Inspect a recording file:

```bash
uv run suolingo-recording inspect ../../tests/fixtures/recordings/synthetic-interruption.json
uv run suolingo-recording inspect path/to/pulled/recordings/*.json
```

When the data models change, regenerate the JSON Schema with `uv run suolingo-recording export-schema`.
A test fails if the committed schema is stale.

### Quest → development server (prepared for Milestone 2)

`localhost` on the Quest refers to the headset. Use the computer's LAN IP address (`ipconfig getifaddr en0` on macOS,
`ipconfig` on Windows), start uvicorn with `--host 0.0.0.0`, and allow the port through the firewall. Wi-Fi client isolation
(common on campus and guest networks) prevents this connection. Development builds allow plain HTTP; release builds do not.

## 5. Troubleshooting

- **The panel says "Hand tracking is not enabled"**: put the controllers down, or enable *Settings → Movement tracking →
  Hand and body tracking* on the headset.
- **No passthrough (black background)**: confirm that the scene's camera background alpha is 0 and that passthrough is
  *Required* in the Meta project configuration. Rerun the setup menu.
- **Package resolution fails for `com.meta.xr.*`**: check access to `https://npm.developer.oculus.com`.
- **Editor starts in Safe Mode**: a compilation error exists. Check the Console, especially after a package version change.
