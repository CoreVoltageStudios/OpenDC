# OpenDC asset licenses and presentation permission

Copyright (c) 2026 Dennis Nilsson for his original material.
Studio credit: **Core Voltage Studios**. This credit does not identify the studio
as a separate legal entity.

## 1. MIT software; separate presentation material

The root [MIT License](https://github.com/CoreVoltageStudios/OpenDC/blob/main/LICENSE) applies to Dennis Nilsson's original engine code
and software documentation, including the importer, simulation, networking,
reusable UI controls, rendering machinery, input/focus handling and persistence.
Existing third-party notices take precedence for their material.

Artwork, recordings and authored presentation content are licensed separately,
including geometry, composition, styling or choreography expressed inside code.
The [file/symbol register](docs/PRESENTATION-LICENSE-MAP.md) identifies that content
and the reusable software alongside it. These are content-specific exceptions,
not reservations of entire UI classes or directories. General algorithms,
functionality, interfaces, techniques, facts and ordinary UI conventions are not
reserved. No exclusive right is asserted over isolated colors, common shapes or
functional values merely because they appear here.

The complete `viewer/DarkColony.Media/` library retains **LGPL-2.1-or-later**,
including the FFmpeg-derived Cinepak decoder; it is not relicensed under MIT.
Preserve its notice, upstream credits and license text.

## 2. Reserved presentation: source-fork and private-use permission

The **Reserved Presentation Material** is Dennis Nilsson's original Landfall and
Relay visual designs and authored presentation sequences; the OpenDC wordmark and
application icon; and the Core Voltage Studios logo, intro animation, signature
sound and associated identity material identified below and in the file/symbol
register. This includes protected expressive material embodied in code. It excludes
the five CC BY cues, third-party material and original Dark Colony content below.

Dennis Nilsson grants everyone a worldwide, non-exclusive, royalty-free permission
under his rights in the Reserved Presentation Material to:

1. Copy, retain, modify and publicly share it in source form as part of OpenDC source
   repositories, public source forks and their source archives, including modified
   forks. The source-form assets listed below may accompany those repositories.
2. Build OpenDC and those forks, and privately run the complete experience, including
   the reserved Landfall and Relay designs and Core Voltage Studios identity sequence.
   Private builds may contain unchanged or modified reserved material.

Keep this notice, the copyright notice, the Core Voltage Studios credit and the
file/symbol register with source copies; identify changes to reserved material.
Source recipients receive these permissions on the same terms without requiring
individual approval for the uses expressly granted above. Listed SVG, ICO, PNG and
WAV files count as source assets for this permission; they do not become freely
redistributable standalone asset packs.

**Public redistribution of an executable, installer, application package or other
build containing Reserved Presentation Material requires separate permission from
Dennis Nilsson.** This permission does not grant that right, nor a general right
to redistribute the material separately or in unrelated works. Other rights in
the reserved material remain reserved, subject to rights applicable law independently
provides. No endorsement, official status or permission to misrepresent a fork as
an official studio release is granted. Keeping the original studio credit in an
authorized source fork or private build is expressly permitted.

These restrictions do not narrow MIT rights in engine code, CC BY rights in the
approved cues, or rights granted by third parties. A build from which reserved
presentation has been removed or replaced is not subject to this special build
redistribution restriction, but all other applicable licenses still apply.
This permission grants only rights Dennis Nilsson controls; it supplies no original
game assets and grants no rights in anyone else's material.

### Separate reserved files

Paths are relative to `viewer/DarkColony.Viewer/`:

| File | Reserved content |
| --- | --- |
| `Branding/CoreVoltage/core-voltage-intro.wav` | Studio signature recording |
| `Branding/Icon/odc-icon.ico` | OpenDC application icon |
| `Branding/Icon/png/odc-icon-512.png` | OpenDC icon image |
| `Branding/Landfall/opendc-wordmark.svg` | OpenDC angular wordmark |

The procedural studio animation and code-embedded Landfall/Relay designs are also
covered as identified in `docs/PRESENTATION-LICENSE-MAP.md`.

## 3. Five approved CC BY 4.0 cues

Dennis Nilsson explicitly approved these five original synthesized recordings for
**Creative Commons Attribution 4.0 International** release on November 6, 2026.
They contain no original-game samples. No directory-wide or future-asset grant is made.

Copyright (c) 2026 Dennis Nilsson. Attribution: **Dennis Nilsson / Core Voltage Studios
— OpenDC Landfall cues**. Source: [OpenDC](https://github.com/CoreVoltageStudios/OpenDC).
License: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/),
[legal code](https://creativecommons.org/licenses/by/4.0/legalcode.en).

| File under `viewer/DarkColony.Viewer/Branding/Landfall/` | Terms |
| --- | --- |
| `cue-residual-hold.wav` | CC BY 4.0 |
| `cue-charge-transfer.wav` | CC BY 4.0 |
| `cue-milestone.wav` | CC BY 4.0 |
| `cue-first-contact.wav` | CC BY 4.0 |
| `cue-awakening.wav` | CC BY 4.0 |

Retain supplied attribution/notices, link to the license and identify modifications
as required by CC BY 4.0. These recordings remain CC BY 4.0 when used in a reserved
scene: its reservation does not revoke or narrow their license. CC BY does not grant
trademark rights. The separate studio intro recording is not one of these five cues.

## 4. Original Dark Colony and Council Wars content

Original game artwork, sound, music, movies, scenarios, scripts and extracted native
tables remain the intellectual property of their respective rights holders. They
are **not** covered by OpenDC's MIT, CC BY or reserved-presentation permissions and
must not be redistributed without authorization. Players supply compatible files
separately. This repository excludes those payloads, original-derived prototype
art, game recordings, imported output and private research extracts.

Importer identifiers, hashes, extraction mappings and converter code do not grant
redistribution rights to the content they process. Importing, decoding or compositing
does not change ownership. Validation establishes compatibility, not ownership or
legal clearance. A locally rendered restoration scene combines project-owned
choreography with proprietary imported graphics; only the former is covered by
project-owned presentation permission.

## 5. Third-party material

Third-party material retains its own terms and attribution, not the project's MIT,
CC BY or reserved-presentation terms. See [dependencies](docs/DEPENDENCIES.md).

| Component | Terms and notices |
| --- | --- |
| Godot 4.7.2 / GodotSharp | MIT and bundled component terms; `packaging/GODOT-LICENSE.txt`, `packaging/GODOT-COPYRIGHT.txt`; export queries actual template notices |
| Self-contained .NET | MIT/component terms; exact runtime pack's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` copied on export |
| `viewer/DarkColony.Media/` | LGPL-2.1-or-later; `NOTICE.txt`, `COPYING.LGPLv2.1`, complete corresponding source and replacement instructions |
| Barlow | SIL OFL 1.1; `Fonts/barlow-OFL.txt` |
| Barlow Semi Condensed | SIL OFL 1.1; `Fonts/barlowsemicondensed-OFL.txt` |
| Share Tech Mono | SIL OFL 1.1; `Fonts/sharetechmono-OFL.txt` |
| Red Hat Display | SIL OFL 1.1; `Fonts/redhatdisplay-OFL.txt` |

Font paths are under `viewer/DarkColony.Viewer/`. Unchanged files and notices are
hash-identified in `Fonts/provenance.json`; font software is not relicensed.
No Godot/runtime/compiler/NumPy executables are checked into this source tree.
Their build-tool/runtime terms remain applicable to their own distributions.
