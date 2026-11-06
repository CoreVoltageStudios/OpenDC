# November 4, 2026 playtest findings

These are reported findings and requested follow-up behavior for the RC1 baseline.
They are not fixed in this source publication. Priorities are the playtest priorities.

## Explained observation: BUG-04

Dennis confirmed that the players could target one another because they were not
allied (BUG-02). There is no separate open friendly-fire defect established by that
observation. Track shared vision separately from alliance state.

## Open bugs

- **BUG-01 — Multiplayer name input reverses typed text** (medium). Characters appear in the typed order; caret and editing remain usable.
- **BUG-02 — Same-team lobby players start without an alliance** (high). Players on the same lobby team start allied. Shared vision is a separate feature/state and must be tested separately.
- **BUG-03 — Units overlap tall terrain scenery** (medium). Visual ordering and anchoring should represent the intended relation to scenery. Investigate drawing order, anchoring and movement before assigning a cause.
- **BUG-05 — Defeated observers retain fog of war** (high). Reveal the full map for defeated observers while keeping all gameplay-changing commands disabled.
- **BUG-06 — Camera bounds prevent centering units near map edges** (medium). Permit sufficient camera overscroll, render outside the map black, and preserve valid battlefield/input bounds. Verify ultrawide and aspect-limited views.
- **BUG-07 — Clearing a joined player name can disconnect the player** (high). Validate the name before connecting, then lock it for the session. Editing must not cause an unintended disconnect.
- **BUG-08 — Multiplayer defaults to the Windows account name** (medium). Let players choose and persist their own OpenDC display name instead of deriving it from the Windows account.
- **BUG-09 — Reduce Motion inherits Windows instead of an explicit OpenDC default** (medium). Use an explicit persistent OpenDC setting, with normal animation as the default. Preserve deliberate user overrides.

## Feature requests

- **FEAT-01 — Navigate the camera by clicking or dragging the minimap** (medium). Navigate the camera while preserving viewport bounds, aspect modes and input isolation.
- **FEAT-02 — Join multiplayer directly as a non-playing observer** (high). An observer joins without consuming a player or map slot. Define observer visibility and permissions explicitly; no gameplay commands.
- **FEAT-03 — Make alliance and shared-vision offers and states clear** (high). Show the sender Awaiting response; show the recipient the sender identity and Accept alliance / Decline. After acceptance both see Allied. Make shared-vision requests and current status equally clear.
- **FEAT-04 — Add team outcomes and useful multiplayer match statistics** (medium). Show team outcome, units and structures destroyed/lost, PETRA-7 collected/spent, and match duration.
