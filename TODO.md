# TODO

## Done

- **Find a good way to distribute this application.**
  `build.sh` cross-compiles every target from one machine, and a `v*` tag builds and
  publishes them to a GitHub release automatically. Builds are self-contained, so users need
  no .NET install; macOS ships as a `.app` bundle.

- **LibVLC is ~300MiB in size, any way to make it smaller?**
  Mostly solved. macOS and Linux no longer ship VLC at all - the app binds to a system
  installation and prompts with a download link if one is missing, so those downloads are
  ~40MB. Windows still bundles the natives, but only for the architecture that can actually
  load them, which took the x64 download from 128MB to 87MB. A second Windows package without
  them is published as well, at 43MB, for people who already have VLC installed.

- **Windows on ARM had no media preview.**
  Investigating showed the original plan - falling back to a system-wide VLC - could not work:
  VideoLAN publishes no Windows arm64 build of VLC, so there is nothing to bundle or to fall
  back to, and an arm64 process cannot load the x64 libraries. The arm64 download was dropped
  instead; Windows on ARM runs the x64 build under emulation with media preview working. The
  Windows fallback to a system-wide VLC install was still added, so the app works when the
  bundled natives are absent, and an arm64 binary now explains the situation rather than just
  offering a VLC download that would not help.

- **Make `.rpyc` preview usable without manual setup.**
  unrpyc can now be downloaded from within the app, and the Python interpreter is detected
  automatically including pyenv installations. Neither has to be located by hand.

## Open

- **macOS builds are not code-signed or notarised.**
  Users have to right-click → Open on first launch. Fixing this needs an Apple Developer
  certificate and signing secrets in CI.

- **Only English is translated.**
  The string table (`Strings.cs`) is structured for more languages; none have been added.

## Investigated and rejected

- **Port the RPYC decompiler into this application** rather than shelling out to unrpyc.
  Measured before attempting: the whole shell-out costs ~100-140ms, of which Python start-up
  is ~10ms, so a port would save well under a tenth of a second per preview - not perceptible
  when clicking a file. Against that, a faithful port is ~4,000 lines covering the AST
  decompiler, screen language 2, ATL, translations and test cases, plus a pickle
  implementation able to fabricate arbitrary Python classes (the .NET pickle libraries expose
  no hook for this, which is exactly why unrpyc ships its own). A real game was measured
  referencing 65 distinct Ren'Py classes, and that surface changes with each Ren'Py release,
  so the port would need continuous maintenance to avoid silently producing wrong output.
  The integration was made effortless instead - see "Done" above.
