# Tall foreground depth checks

Refs #3. Ground rendering retains the native per-column cutoff, with one refinement:
after the ground anchor passes the southernmost pixel of a connected tall foreground
shape, that shape stops masking the body and its shadow. FIN bobbing does not move
this decision. Pathfinding, collision, orders, simulation timing, visibility and
saves are unchanged. This is a presentation refinement, not a claim of original
executable behavior.

The shape map is prepared once when loading terrain. Eight-connected shapes at
least 32 source pixels tall receive a base depth. Small detached details retain
the native mask; touching shapes conservatively share their deepest base. The
reference `Hidden`/`Cutoff` APIs retain their formulas. `MayMask` remains conservative.

Content-free checks:

```powershell
dotnet run --project viewer/DarkColony.ImportChecks -- terrain-depth
```

These cover front/behind boundaries, separate upper/lower shapes, transparent holes,
diagonal connectivity, bounds, flipped foreground, shadows, FIN offsets, deterministic
replacement, conservative fast-path rejection and allocation-free depth lookup.

For rendered checks, set `$Godot` to your Godot .NET executable and `$Store` to your
imported content store. Use an isolated profile. These checks stage a subject and
remove unrelated entities; they are not ordinary-play or performance evidence.

```powershell
dotnet build viewer/DarkColony.Viewer --nologo /p:Optimize=true
$env:ODC_USER_DATA = Join-Path $PWD 'generated/scenery/profile'
& $Godot --path viewer/DarkColony.Viewer --windowed --resolution 1920x1080 -- `
  --main-menu --skip-intro --capture --mission-suite=scenery-occlusion `
  "--content-store=$Store" "--output=$PWD/generated/scenery/front"
```

Repeat with `--scenery-behind` and another output directory for an approach from
behind the Bloody Cross spire. `--scenery-no-capture` skips temporal PNGs while
retaining rendered texture checks and state traces.

The primary route is Bloody Cross (`D4PLAY09`), near logical cell (45,12): start
at source-pixel anchor (1466,3176), MoveOnly to (47,12), return to (44,12) at update 60.
The second route uses the tall canopy in 4 Kingdoms (`J4PLAY01`): start (33,29),
move toward (39,29), reverse at update 60. Each runs 120 updates. The behind approach
starts at (46,14). Body textures are checked against the complete mask including
the sampling gutter. The reported front pose must stay visible; canopy and behind
paths must still exercise masking. Returning to the menu releases map-specific depth.

Traces record coordinates and SHA-256 of the full save payload excluding only
`runId`, plus the authoritative event stream hash. Compare matching ticks between
builds. The primary path must remain walkable. The jungle path retains its earlier
short quantized corner excursions; this rendering change must not alter them.

Development-machine verification: identical normalized state/events at all 240
matched before/after ticks; 1080p controlled rendered paths plus the behind approach.
No real Windows input, full-campaign, LAN or human acceptance is implied. Original
content and local comparison clips are excluded from the public repository.

Retained mask storage changes from one byte to two bytes per map pixel: about 14 MiB
extra for these 4096x3584 maps. Constructor-only warmed medians were 26-31 ms versus
4-5 ms before. Preparation occurs during map loading; drawing adds no flood fill or
per-pixel allocation. These measurements are not gameplay FPS measurements.
