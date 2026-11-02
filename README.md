# unity-hackathon

Unity hackathon game. The initial URP scene is an environment smoke test;
the game concept and final target platforms are not selected yet.

## Development environment

- Unity Editor **6000.6.7f1** on every development/build machine.
- URP **17.6.0** and Unity Test Framework **1.8.0**.
- Git and Git LFS; Python 3.9+ for repository/test scripts.
- Open the repository root in Unity Hub (it contains `Assets`, `Packages`,
  and `ProjectSettings`). Open `Assets/Scenes/SampleScene.unity` to inspect the scene.
- `Force Text` serialization and `Visible Meta Files` are enabled.
- Commit `Assets` with `.meta` files, `Packages` including its lock file,
  and `ProjectSettings`. Do not commit `Library` or test reports.

Fedora is used for this initial local verification but is not an officially
supported Unity Editor distribution. The Macs must use the same full Editor
version. No Windows/Android player build or cross-platform runtime test has
been completed by this initial setup.

## Repository checks

Stage the intended files, then run from the repository root:

```sh
python3 scripts/check_repository.py
git lfs fsck
```

GitHub Actions runs these checks on pull requests and pushes to `main`.

## Unity tests

Close this project's Unity Editor before invoking batch-mode tests:

```sh
python3 scripts/run_unity_tests.py
```

The script finds the pinned Editor in standard Unity Hub locations on Linux,
macOS, and Windows. For a custom location:

```sh
python3 scripts/run_unity_tests.py --editor /absolute/path/to/Unity
```

Use `--platform EditMode` or `--platform PlayMode` to run one suite.
By default the graphics device is disabled; `--graphics` enables it on a
machine with a working display/graphics setup. Headless success does not
validate rendering, touch input, or performance on target devices.

- **EditMode:** pinned Editor version, enabled build scenes, Git-compatible
  serialization, and the URP pipeline resolved for the active quality level.
- **PlayMode:** the first build scene loads, has an active camera, and has
  no missing scripts or unexpected errors.
- Results: `TestResults/EditMode.xml`, `TestResults/PlayMode.xml`, and logs.
- A nonzero Editor exit, missing XML, failed tests, or no passed tests makes
  the command fail. Old XML is removed before each run.

### Verification and launcher limitation

On Fedora with Unity 6000.6.7f1, direct headless Editor runs passed all four
EditMode tests and the one PlayMode test. No target-player or visual checks
were performed. In the restricted automation environment, launching Unity as
Python's child process exited with SIGKILL before tests; the Python convenience
command has therefore **not** been verified end to end in that environment.
Its result-validation logic was checked against passing, failing, empty,
malformed, missing/stale reports and a nonzero Editor exit.

For a direct run from the repository root on Fedora (close the project's Editor first):

```sh
"$HOME/Unity/Hub/Editor/6000.6.7f1/Editor/Unity" \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode.xml" \
  -logFile "$PWD/TestResults/EditMode.log"
"$HOME/Unity/Hub/Editor/6000.6.7f1/Editor/Unity" \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode.xml" \
  -logFile "$PWD/TestResults/PlayMode.log"
```

Adapt the executable and project paths on other machines. Do not add `-quit`:
the Test Framework exits after writing its results. Remove old XML reports
before a manual run and check both the process exit code and the new report.
Alternatively, open **Window > General > Test Runner** in the Editor and run
both the EditMode and PlayMode tabs.

Cached assembly reload also stalled in this environment. With Unity closed,
moving `Library/ScriptAssemblies` to an ignored backup under `artifacts/` forced
recompilation and allowed direct runs to complete. The original `Library`
and later script-cache backups remain in `artifacts/`; these are local,
regenerable caches, not project source. The underlying cause is not established.

These are real project smoke tests, not gameplay or multiplayer coverage.
Unity tests currently run locally. The GitHub workflow only runs repository
checks until a licensed Unity runner is configured in the next setup step.

## Planned build targets

Windows x64 (initially Mono), Android ARM64 (IL2CPP), and Linux x86-64
Dedicated Server. Build modules, profiles, automation, and device validation
are separate setup steps; listing these targets does not mean they are ready.
