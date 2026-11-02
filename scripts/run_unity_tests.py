"""Run real Unity tests and reject missing, empty, or failed XML reports."""

import argparse
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", help="Path to the Unity executable")
    parser.add_argument("--platform", choices=("EditMode", "PlayMode", "all"), default="all")
    parser.add_argument("--timeout", type=int, default=900, help="Seconds per test suite")
    parser.add_argument("--graphics", action="store_true", help="Initialize the graphics device")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    version_file = root / "ProjectSettings/ProjectVersion.txt"
    version = next(line.split(":", 1)[1].strip() for line in version_file.read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    candidates = [
        Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity",
        Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity"),
        Path(os.environ.get("ProgramFiles", "C:/Program Files"))
        / f"Unity/Hub/Editor/{version}/Editor/Unity.exe",
    ]
    explicit = args.editor or os.environ.get("UNITY_EDITOR_PATH")
    editor = Path(explicit) if explicit else next((p for p in candidates if p.is_file()), None)
    if editor is None or not editor.is_file():
        parser.error(f"Unity {version} not found. Pass --editor or set UNITY_EDITOR_PATH.")
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    results = root / "TestResults"
    results.mkdir(exist_ok=True)
    platforms = ("EditMode", "PlayMode") if args.platform == "all" else (args.platform,)
    for platform in platforms:
        report = results / f"{platform}.xml"
        log = results / f"{platform}.log"
        report.unlink(missing_ok=True)  # Never accept a report left by an older run.
        command = [str(editor), "-batchmode", "-projectPath", str(root),
                   "-runTests", "-testPlatform", platform, "-testResults", str(report),
                   "-logFile", str(log)]
        if not args.graphics:
            command.append("-nographics")
        # Do not pass -quit: the Test Framework exits after it writes results.
        print(f"Running {platform} with Unity {version}; log: {log}", flush=True)
        try:
            completed = subprocess.run(command, cwd=root, timeout=args.timeout)
        except subprocess.TimeoutExpired:
            print(f"ERROR: {platform} timed out. See {log}", file=sys.stderr)
            return 1
        if completed.returncode != 0:
            print(f"ERROR: Unity exited with {completed.returncode}. See {log}", file=sys.stderr)
            return 1
        if not report.is_file():
            print(f"ERROR: Missing test report: {report}", file=sys.stderr)
            return 1
        try:
            document = ET.parse(report).getroot()
            cases = document.findall(".//test-case")
            passed = sum(case.get("result") == "Passed" for case in cases)
            failed = sum(case.get("result") == "Failed" for case in cases)
            valid = document.get("result") == "Passed" and passed > 0 and failed == 0
        except (ET.ParseError, OSError) as error:
            print(f"ERROR: Cannot read test report: {error}", file=sys.stderr)
            return 1
        print(f"{platform}: passed={passed}, failed={failed}, total={len(cases)}", flush=True)
        if not valid:
            print(f"ERROR: Tests failed or no tests passed. See {report}", file=sys.stderr)
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
