"""Repository checks only; Unity and gameplay tests are separate CI jobs."""

import re
import subprocess
import sys
from pathlib import Path


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True).stdout


root = Path(git("rev-parse", "--show-toplevel").decode().strip())
tracked = {p.decode("utf-8") for p in git("ls-files", "-z").split(b"\0") if p}
errors = []
required = {
    "README.md", ".gitignore", ".gitattributes", ".editorconfig",
    "scripts/check_repository.py", ".github/workflows/repository-checks.yml",
}
for name in sorted(required - tracked):
    errors.append(f"Required file is not tracked: {name}")

for directory in (
    "Library", "Temp", "Obj", "Build", "Builds", "Logs", "UserSettings",
    "MemoryCaptures", "Recordings", "TestResults", "artifacts",
):
    for name in sorted(tracked):
        if name.split("/", 1)[0].lower() == directory.lower():
            errors.append(f"Generated file is tracked: {name}")
    probe = f"{directory}/repository-check-probe.txt"
    result = subprocess.run(
        ["git", "check-ignore", "--no-index", "-q", "--", probe], cwd=root
    )
    if result.returncode != 0:
        errors.append(f"Directory must be ignored: {directory}/")

for probe in (
    "Assets/Probe.cs", "Assets/Probe.cs.meta", "Assets/Scenes/Probe.unity",
    "Assets/Scenes/Probe.unity.meta", "Assets/Textures/Probe.png",
    "Packages/manifest.json", "Packages/packages-lock.json",
    "ProjectSettings/ProjectVersion.txt",
):
    result = subprocess.run(
        ["git", "check-ignore", "--no-index", "-q", "--", probe], cwd=root
    )
    if result.returncode != 1:
        errors.append(f"Source path must not be ignored: {probe}")

text_extensions = {
    ".cs", ".py", ".sh", ".yml", ".yaml", ".json", ".md", ".txt",
    ".unity", ".prefab", ".meta", ".asset", ".asmdef", ".asmref",
    ".shader", ".hlsl", ".cginc", ".uxml", ".uss",
}
# Opening/closing markers, not =======: the latter is valid Markdown.
conflict = re.compile(r"^(?:<{7,}|>{7,}|\|{7,})(?:\s|$)")
for name in sorted(tracked):
    path = root / name
    if path.suffix.lower() not in text_extensions and name not in required:
        continue
    if not path.is_file():
        errors.append(f"Tracked file is missing from the working tree: {name}")
        continue
    try:
        with path.open(encoding="utf-8-sig") as stream:
            for number, line in enumerate(stream, 1):
                if conflict.match(line):
                    errors.append(f"Merge conflict marker: {name}:{number}")
    except UnicodeDecodeError:
        # Some Unity .asset files are binary even with text serialization enabled.
        if path.suffix.lower() != ".asset":
            errors.append(f"Expected UTF-8 text: {name}")

if errors:
    print("Repository checks FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Repository checks passed. Unity/gameplay tests have not run.")
