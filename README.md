# OpenDC — A modern engine for Dark Colony.

OpenDC is an independent restoration project by Core Voltage Studios. This is the
**1.0.0 RC1 playtest source baseline**, including the base campaigns, optional
Council Wars campaigns, training, Single-player War and LAN/direct-IP Multiplayer
War. It retains the RC1 playtest behavior; known bugs are tracked in
[GitHub Issues](https://github.com/CoreVoltageStudios/OpenDC/issues).

## Bring your own game

No original game assets are included. Supply a legally obtained compatible copy
of **Dark Colony, English EU (DCUK)**. The verified original EU and Sold Out game
trees share the supported mapping. **English Sold Out Council Wars** is optional:
import it initially or add it later through **Options → Game content**. The base
game works independently. US, other languages and unknown executable hashes are
not supported by this baseline; the importer explains unsupported/incomplete input.

Select a CUE with its BIN track files, a supported MDS/MDF image, a data-only ISO,
a complete extracted/mounted disc folder, or a compatible ZIP. A containing folder
can be inspected. Installed-game folders may lack movies and briefing voices.
Select the full source when requested. Physical-CD audio extraction is unsupported;
use an image with audio tracks for music. Missing CD music does not disable voices,
effects or gameplay. Missing PP.AVI and Human training lesson-6 result text have
explicit fallbacks. Sources are read only; original executables are never run.

Original content belongs to its respective rights holders. OpenDC is independent;
compatibility validation is not proof of ownership or legal clearance.

## Build and run on Windows x64

Build tools (not player runtime dependencies): Git, a .NET SDK able to build net8.0
(test configuration: .NET SDK 10.0.403), **Godot 4.7.2 .NET** and its matching .NET
Windows export templates. The non-.NET Godot editor is insufficient. NuGet access
is required for the first dependency restore. Use PowerShell from this checkout.
Set `$Godot` to your unpacked **editor executable**, not the `_console.exe` wrapper.

```powershell
$Godot = 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64.exe'
dotnet build ./viewer/DarkColony.Viewer/DarkColony.Viewer.csproj --nologo /p:Optimize=true
& $Godot --headless --editor --path ./viewer/DarkColony.Viewer --import
./tools/run_imported.ps1 -Godot $Godot -NoBuild -Setup `
  -PlayerData ./generated/play-profile -ContentStore ./generated/content-store
```

The source helper deliberately uses an isolated profile. Reuse the same arguments
without `-Setup` for normal menu launch. It never enables development-content
fallback. Follow content setup, confirm your source/destination, then enter play.
Keep at least 7 GiB free for staging. User settings/saves and imported content have
separate lifecycles. Do not store original sources or imports inside a distributable.
Bootstrap fonts, icon, wordmark and synthesized sound are included: no Python or
external media converter is needed to launch or import.

## Export and installer

Install the matching Godot export templates first. Export destinations must be new
and outside this checkout. Python 3 (standard library only) is used for the payload
audit; Inno Setup **7.1.1** is required only when building an installer.

```powershell
./tools/export_engine_only.ps1 -Godot $Godot `
  -Destination "$env:TEMP\OpenDC-rc1-export" `
  -DisplayVersion '1.0.0 RC1' -SemanticVersion '1.0.0-rc.1' -NumericVersion '1.0.0.1'
python ./tools/audit_engine_export.py "$env:TEMP\OpenDC-rc1-export\Engine" "$env:TEMP\OpenDC-rc1-export\Evidence"
& "$env:TEMP\OpenDC-rc1-export\Engine\OpenDC.exe"
```

To build a local installer from a clean committed tree (choose fresh paths):

```powershell
./tools/build_engine_installer.ps1 -Godot $Godot `
  -ISCC 'C:\Tools\InnoSetup\ISCC.exe' `
  -ExportRoot "$env:TEMP\OpenDC-rc1-installer-export" `
  -Destination "$env:TEMP\OpenDC-rc1-installer"
```

The export includes its self-contained .NET runtime, Godot, required notices and
replaceable media-library source. Players need none of these development tools.
Normal exported profiles use `%LOCALAPPDATA%\OpenDarkColony`, with the default
content library in its `Content` subdirectory. These compatibility names are
intentional. App files need no ordinary-play writes. See [dependencies](docs/DEPENDENCIES.md).

## Checks

With your own source supplied separately, the existing diagnostic CLI can inspect,
import and validate an isolated library:

```powershell
dotnet run --project ./viewer/DarkColony.ImportChecks -c Release -- inspect ./generated/check-content 'D:\MyDarkColony.cue'
dotnet run --project ./viewer/DarkColony.ImportChecks -c Release -- import ./generated/check-content 'D:\MyDarkColony.cue'
dotnet run --project ./viewer/DarkColony.ImportChecks -c Release -- validate ./generated/check-content
dotnet run --project ./viewer/DarkColony.ImportChecks -c Release -- inventory ./generated/check-content ./generated/inventory
```

`inventory` initializes/restores available campaign/training entries and all War
maps. It is not a full playthrough. Other recovery diagnostics deliberately corrupt
**disposable** imports; never point them at your normal library. Developer fixtures
and proprietary test inputs are not distributed.

Multiplayer uses direct LAN/virtual-LAN IPv4 and UDP 27960, up to eight occupied
slots. Both computers need compatible builds and separately supplied content.
There is no host migration, joining in progress, reconnect, multiplayer save/load
or restart. Local menus do not pause a network match; host synchronized pause
remains separate. OpenDC does not change firewall or router rules.

## Original release credits

Historical notices transcribed separately from each title's preserved original
materials; names, years and wording are retained as printed.

**Dark Colony**

> ©1997 Strategic Simulations, Inc® A Mindscape Company. All rights Reserved
>
> ©1997 Take 2 Interactive Software Europe. Inc.

**Dark Colony: The Council Wars**

> ©1997 Strategic Simulations, Inc® A Mindscape Company. All rights Reserved
>
> ©1997 Take 2 Interactive Software Europe. Inc.

Both titles' original credits list **Take 2 Interactive Software Canada** and,
under **STRATEGIC SIMULATIONS**, **PRODUCER: Rick White** and
**ASSOCIATE PRODUCER: Jon Kromrey**.

These historical credits do not establish current ownership or grant permission
to redistribute original game content. OpenDC is an independent project.

## License and reports

OpenDC's original engine code is [MIT licensed](LICENSE), copyright Dennis Nilsson.
Studio credit: **Core Voltage Studios**. Original Landfall/Relay presentation and
studio identity material are reserved, with explicit permission to retain them in
public source forks, build the project and privately run the complete experience.
**Public redistribution of a build containing reserved presentation requires
separate permission.** This restriction does not narrow the MIT engine license.

The five identified Landfall cues are CC BY 4.0. Original Dark Colony content is
excluded; third-party terms remain applicable, including LGPL-2.1-or-later for the
media library and OFL 1.1 for fonts. Read [asset terms](ASSET-LICENSES.md), the
[file/symbol map](docs/PRESENTATION-LICENSE-MAP.md), and
[dependency notices](docs/DEPENDENCIES.md). Source availability does not grant
endorsement or an unrestricted license to studio branding.

Report bugs using [GitHub Issues](https://github.com/CoreVoltageStudios/OpenDC/issues).
Include build, mission/mode, steps, expected/observed behavior and relevant logs.
Do not attach original game assets, disc images or private profile information.
RC1 implementation and automated checks do not establish full human campaign,
clean-Windows or two-computer LAN/Hamachi acceptance. See
[November 4 playtest findings](docs/NOVEMBER-4-PLAYTEST.md).
