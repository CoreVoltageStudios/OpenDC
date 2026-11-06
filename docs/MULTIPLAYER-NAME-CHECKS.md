# Multiplayer name regression

Build the source using the README instructions and import your own supported base
content separately. This check reads that content without changing it. Use fresh,
isolated output/profile directories: old barrier files invalidate the coordination.
No original game assets or test profiles are distributed with the tests.

Open two PowerShell terminals from the repository root. In **both**, set these
variables to the same paths (the Godot .NET executable from your source build):

```powershell
$godot = 'C:\Tools\Godot\Godot.exe'
$content = (Resolve-Path './generated/check-content').Path
$run = [IO.Path]::GetFullPath('./generated/name-check-01')
```

In the first terminal:

```powershell
New-Item -ItemType Directory -Force "$run/host", "$run/barrier" | Out-Null
$env:ODC_USER_DATA = "$run/host-profile"
& $godot --path ./viewer/DarkColony.Viewer --windowed --resolution 1920x1080 -- `
  --main-menu --skip-intro --capture --mission-suite=network-names `
  --network-role=host "--barrier=$run/barrier" "--content-store=$content" "--output=$run/host"
```

Start the second promptly (each barrier has a 30-second timeout):

```powershell
New-Item -ItemType Directory -Force "$run/client" | Out-Null
$env:ODC_USER_DATA = "$run/client-profile"
& $godot --path ./viewer/DarkColony.Viewer --windowed --resolution 1920x1080 -- `
  --main-menu --skip-intro --capture --mission-suite=network-names `
  --network-role=client "--barrier=$run/barrier" "--content-store=$content" "--output=$run/client"
```

The two applications use actual ENet on localhost UDP 27974. Their existing
menus/control bindings receive Godot-dispatched input; this is not physical
Windows keyboard input or LAN acceptance. Connected player names remain read-only;
host-owned setup names remain editable. Empty/whitespace pre-connect names, rapid
typing, middle insertion, selection replacement, and focused rebinding are checked.
A diagnostic receive gate briefly holds actual incoming packets on the host while
a newer incomplete AI-name draft is typed. Releasing it exercises the normal
receive/configure/publish/UI-refresh path and asserts the draft, caret and selection
survive. The gate is off in normal sessions. The client then receives the completed
AI name, and both peers ready/start a match and advance to frame 128.

Each process must exit 0 with `MAIN_MENU_SUITE_PASS network-names` and all entries
in `checks.json` passing. Compare the `frame128.json` files byte-for-byte. The
`names.jsonl` records and PNG captures support inspection. Keep these generated
original-derived captures local. Repeat with a new `$run` path; no cleanup of
existing evidence or player data is required. Close these terminals afterward so
the temporary profile override is not reused for ordinary play.

This check does not cover saved display-name preferences (#7), a two-computer
playtest, or any post-1.0 multiplayer feature.
