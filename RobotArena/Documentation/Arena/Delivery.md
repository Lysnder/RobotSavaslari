# Arena layouts and environment delivery

Issue: https://github.com/Lysnder/RobotSavaslari/issues/32
Branch: `feature/arena-layouts`; PR target: `test` only.
Starting test SHA: `7bc407a4f375f7e522736158049a33b458719096`.
Fetched main: `8452fa4a9f568966c98f5a2468ba57436ee05624`.
Open PR checked at start: #37 combat camera/replay, not used as a dependency.

## Expanded grid

MainArena retains its GUID, robots, MatchController, UI and existing camera.
The existing environment builder now expands that scene in place. Original floor
panel geometry/scale and 2 m spacing are retained: 8x8, 64 cells, 16x16 m floor.
The continuous collider top stays y=.82. Perimeter centers are +/-8 m; the .19 m
inset includes the .17 m corner half-width and .02 m clearance. The existing
rotation-independent robot radius is subtracted from this shared inner perimeter.
The original spawn transforms are retained. ArenaBounds sets the existing public
bot limits after runtime bot creation, without changing AI source.

Verification on Unity 6000.4.4f1 / Windows x64:
- Geometry EditMode tests: 2/2, including repeated production generation.
- Production Editor physical keyboard traversal: two rounds, eight perimeter
  waypoints per round, old bounds crossed, no floor fall-through or perimeter exit.
- Rotation clamp regression: 12 headings, outward velocity removed.
- Runtime bot extents and clean second-round spawn/result reset verified.
- Windows build succeeded; same executable traversal passed in two rounds.

Evidence: `before-expansion.png`, `after-expansion.jpg`, `boundary-editor.mp4`,
`boundary-windows.mp4`, `geometry-editmode.json`, and Editor/Windows result JSON.
The opt-in `-arena-boundary-check -arena-capture` harness only runs when requested;
normal players do not get test objects or injected input.

## Ownership

Changes are restricted to arena geometry/builders, new arena runtime/editor/tests,
and arena fields / ConstrainToArena in RobotPrototypeController. Motor, drive
bridge, bot, match and menu/settings source files are not edited. Raw verification
frames and local tools remain ignored. No Windows distribution or Steam upload.

Further layout/environment acceptance will be appended as it is completed.
