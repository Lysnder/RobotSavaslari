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

## Open and Tactical layouts

ArenaLayout assets under Settings/Arena use the same fixed 8x8/2 m grid.
The existing MainArenaSceneBuilder wires the arena-owned environment component;
it does not create a parallel match manager. Ready selection is bottom-left and
checks the existing settings/menu enabled state. Selection during play is refused.
Only Layout Contents is retired on regeneration; colliders are disabled before
deferred destruction. Reset preserves layout and existing generated objects.

Validation expands obstacle rectangles by the full measured turning radius plus
.05 m, checks .5 m configuration-space connectivity and rejects isolated pockets,
invalid/duplicate cells, incompatible spawn content and invalid perimeter walls.
Tactical requires a detour (33 samples vs Open 25 at measured radius 1.061916 m).

27 arena/reference EditMode cases passed. Editor and Windows x64 each passed
two genuine health-ended matches per layout, real keyboard route traversal,
selection guards, Open→Tactical→Open cleanup and second-round persistence.
Evidence: layout-open.png / layout-tactical.png with green clearance routes,
layouts-editor.mp4 / layouts-windows.mp4, layout-editmode.json and Layouts JSON.
An interrupted fixture initially leaked temporary InputSettings; cleanup is now
idempotent on finish, disable and destroy. Nested coroutine failures are reported
as failures. The final successful runs used a clean Editor session and the corrected
approach endpoint outside the occupied opponent spawn.

## Warning barriers

One cell (6,6) in each layout uses a non-damaging kinematic barrier. Durations:
idle 1.2 s, warning 1.4 s, rise .8 s, active 1.5 s, lower .8 s. Occupancy at the
centre or edge postpones rising and cancels after 2 s of occupancy following the
warning. Occupancy during rising disables the collider and lowers the barrier.
Spawns cannot contain hazards; clearance validation treats raised barriers as
obstacles. No robot teleport or launch force is used by the environment.

Three lifecycle/occupancy EditMode tests passed. Editor and Windows passed actual
centre/edge occupancy, pause freeze, three consecutive cycles, two matches and
single-instance/reset checks. The final Windows log has no managed exception.
Initial startup reset before preview initialization is explicitly safe. The test
wait includes idle + warning + occupancy timeout; an earlier short fixture wait
was corrected before recording final results.
Evidence: barrier-editmode.json, Barriers result JSON, barriers-editor.mp4 and
barriers-windows.mp4 (continuous warning/rise/active/lower cycles).

## Conveyor surfaces

Cells (2,1) and (3,1) carry robots along the visible arrow at .8 m/s surface
velocity. The contribution is removed at execution order 105 before the existing
motor (110), then applied at 120. No motor or drive bridge source changes.
Contacts are recomputed every step; compound colliders and adjoining surfaces
resolve to one contribution per robot. A stopped wall contact does not receive a
reverse cleanup kick. Exit stops application by the next physics step; the
motor's braking momentum is reported separately. Disable and health reset clear
the transient contribution and contacts.

Editor and Windows x64 passed equivalent player/bot transport (both .46448 m in
.6 s), real keyboard entry/traversal/exit, compound colliders, adjacent transition,
corner contact, bounded idle speed, reverse (-2.16294 m/s), next-step exit,
disable and two-match reset. Thirty arena/reference EditMode cases passed,
including nonfinite/zero conveyor directions. Final Windows run exits without
managed exceptions; teardown uses cached health references safely.
Evidence: conveyor-editmode.json, Conveyors result JSON and conveyors-*.mp4.
Test-only controlled placements isolate contact/exit cases; production transport
uses rigidbody velocity and the existing motor, never robot transform teleport.
