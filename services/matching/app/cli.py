"""Command-line tools for recordings and contracts.

uv run suolingo-recording inspect path/to/recording.json [--json]
uv run suolingo-recording export-schema [--check]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from pydantic import ValidationError

from app.recordings.io import load_recording
from app.recordings.quality import QualityReport, analyze
from app.schemas.recording import SCHEMA_VERSION, HandRecording

REPO_ROOT = Path(__file__).resolve().parents[3]
SCHEMA_PATH = REPO_ROOT / "contracts" / "hand-recording" / "hand-recording.schema.json"


def build_json_schema() -> str:
    schema = HandRecording.model_json_schema(by_alias=True)
    schema["$schema"] = "https://json-schema.org/draft/2020-12/schema"
    schema["$id"] = f"https://suolingo.dev/contracts/hand-recording/{SCHEMA_VERSION}"
    schema["title"] = "Suolingo hand recording"
    return json.dumps(schema, indent=2, ensure_ascii=False) + "\n"


def _format_report(path: Path, report: QualityReport) -> str:
    def opt(value: float | None, fmt: str) -> str:
        return "n/a" if value is None else format(value, fmt)

    lines = [
        f"Recording {report.recording_id}  ({path.name})",
        f"  frames: {report.frame_count}  duration: {report.duration_seconds:.3f}s "
        f"(requested {report.requested_duration_seconds:.2f}s)",
        f"  frame interval: mean {opt(report.mean_frame_interval_seconds, '.4f')}s, "
        f"max {opt(report.max_frame_interval_seconds, '.4f')}s, "
        f"rate ~{opt(report.effective_rate_hz, '.1f')} Hz",
        f"  head tracked frames: {report.head_tracked_frames}/{report.frame_count}",
    ]
    for side in ("left", "right"):
        q = report.hand(side)
        lines.append(
            f"  {side:<5}: tracked {q.tracked_frames}/{report.frame_count} "
            f"({q.tracked_ratio(report.frame_count):.0%}), high-confidence {q.high_confidence_frames}, "
            f"interruptions {q.interruptions}, longest gap {q.longest_untracked_seconds:.3f}s"
        )
        for gap in q.gaps:
            status = "resumed" if gap.ended else "not resumed by end"
            lines.append(
                f"         gap t={gap.start_t:.3f}..{gap.end_t:.3f}s ({gap.frames} frames, {status})"
            )
    lines.append("  warnings: " + (", ".join(report.warnings) if report.warnings else "none"))
    return "\n".join(lines)


def _cmd_inspect(args: argparse.Namespace) -> int:
    exit_code = 0
    for raw in args.files:
        path = Path(raw)
        try:
            recording = load_recording(path)
        except (OSError, ValidationError, ValueError) as error:
            print(f"{path}: INVALID\n{error}", file=sys.stderr)
            exit_code = 1
            continue
        report = analyze(recording)
        if args.json:
            print(json.dumps({"file": str(path), "report": report.to_dict()}, indent=2))
        else:
            print(_format_report(path, report))
    return exit_code


def _cmd_export_schema(args: argparse.Namespace) -> int:
    content = build_json_schema()
    if args.check:
        current = SCHEMA_PATH.read_text(encoding="utf-8") if SCHEMA_PATH.exists() else ""
        if current != content:
            print(f"{SCHEMA_PATH} is stale; run: uv run suolingo-recording export-schema", file=sys.stderr)
            return 1
        print(f"{SCHEMA_PATH} is up to date")
        return 0
    SCHEMA_PATH.parent.mkdir(parents=True, exist_ok=True)
    SCHEMA_PATH.write_text(content, encoding="utf-8")
    print(f"Wrote {SCHEMA_PATH}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="suolingo-recording")
    sub = parser.add_subparsers(dest="command", required=True)

    inspect = sub.add_parser("inspect", help="Validate recordings and print input-quality reports")
    inspect.add_argument("files", nargs="+")
    inspect.add_argument("--json", action="store_true", help="Print machine-readable JSON")
    inspect.set_defaults(func=_cmd_inspect)

    export = sub.add_parser("export-schema", help="Write the JSON Schema to contracts/hand-recording/")
    export.add_argument("--check", action="store_true", help="Fail if the committed schema is stale")
    export.set_defaults(func=_cmd_export_schema)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
