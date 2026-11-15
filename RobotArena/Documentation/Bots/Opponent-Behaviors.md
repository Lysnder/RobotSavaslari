# Opponent behavior delivery

Branch: `feature/opponent-behaviors`, created after `git pull --ff-only origin main`.
Base main: `8452fa4a9f568966c98f5a2468ba57436ee05624`.
Initial origin/test: `7bc407a4f375f7e522736158049a33b458719096` (identical content tree).
The user's main-based branch instruction overrides the attachment's test-based branch instruction.

## Runtime integration

The existing `RobotBotController` remains the sole intent producer. No scene, prefab,
motor, weapon, health, GameSettings, Prototype or MatchController asset was changed.
`OpponentSelectionPanel` attaches once to MainArena's existing menu at scene load.
It never adds a bot: the normal ConfigureCombat path creates and configures it.
The selection is applied before the first bot physics tick and once per new match.
Selection is offered at the lower right of Ready only, with settings closed and
the menu enabled. Missing selection keeps Aggressive/Hard defaults usable.

| Combination | Approach | Decision interval at default reactionSeconds | Aim error bound | Position error bound |
|---|---|---|---|---|
| Easy Aggressive | Close gap, full existing pressure window | 0.40 s | 12 degrees | 0.45 m |
| Hard Aggressive | Close gap, full existing pressure window | 0.16 s | 2 degrees | 0.08 m |
| Easy Counter | Bounded angle-seeking pass, then commit | 0.40 s | 12 degrees | 0.45 m |
| Hard Counter | Bounded angle-seeking pass, then commit | 0.16 s | 2 degrees | 0.08 m |

Easy can miss 15% of subsequent decision samples; Hard does not. Each bot owns a
seeded System.Random, default seed 740. These are decision settings, not health,
damage or physical capability multipliers. Counter uses 65% of the existing
pressure window and cannot immediately repeat its entry maneuver after it ends.

## Observation and memory

Threat uses planar relative Rigidbody velocity, closing speed, time and distance
of closest approach, plus target facing. It requires closing speed >0.8 m/s,
the target's own approach speed >0.7 m/s, a closest approach within 0.85 s and
1.35 m, and separation >1.05 m. Stationary targets pursued by the bot, retreating
targets, offset passes and already-close contact do not count as new charges.
AI never reads the player's keyboard. Verification scripts may inject keyboard
events to exercise the actual player driving path.

Memory is a six-entry ring of distinct charge onsets in 60-degree local-angle
sectors. A charge ends after 0.30 s without an incoming observation. Entries
expire after 12 s of unpaused simulated time. At least three retained onsets
and a two-thirds share in the current sector invert the preferred evasion side.
Different attack angles dilute that share; one sample never establishes a habit.

Evasion tests both lateral destinations with overlap and swept box physics
queries, bounded by the existing public tacticalHalfExtents. Saturated query
buffers fail closed. With neither side safe the bot stops its approach briefly.
Existing wall recovery has priority over evasion. Committed maneuvers expire,
so unsafe paths cannot create an indefinite retreat state.

Damage decisions read CurrentHealth/MaxHealth, WeaponOperational, LostWheels,
Mobility and Turning. No AI code writes health or part damage. Unarmed bots
emit no weapon intent or attack event and can still ram via the shared motor.
Limited mobility avoids wide flank/evasion maneuvers. Low health shortens
pressure; a disarmed or critically damaged opponent encourages pressure.
Restore, new match and invalid targets clear decision state and memory.
Settings pause preserves timers, memory and random sequence and produces no attack.

## Reproduction

- EditMode: `RobotSavaslari.Tests.BotDecisionTests`.
- PlayMode: `RobotSavaslari.Tests.BotControllerTests`.
- MainArena Play Mode: add `BotBehaviorSmoke` to a temporary GameObject. Inspect
  `Completed`, `Passed`, `Report` and `BOT_BEHAVIOR` console lines.
- Windows x64: existing BuildWindowsPlayer.Build, then run
  `RobotSavaslari.exe -bot-behavior-smoke -logFile <absolute-log-path>`.
  It runs four combinations, two physical keyboard-driven matches per combination,
  with real regional damage/restore, late bot creation and pause/selection gates.
- Existing regressions: BotScenarioChecks in BotScenarios; BotTacticsChecks and
  RobotDamageChecks in the existing CombatArena test scene.
- Add `-bot-evidence-dir <absolute-frame-directory>` for offscreen arena camera
  PNGs and a matching decision TSV. Convert with
  `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/BotEvidence/ConvertTo-MjpegAvi.ps1 -FramesDirectory <directory> -OutputPath <video.avi>`.
  Captures use a nominal 5 fps cadence; the AVI has no audio or screen-space HUD.
  Each TSV row contains frame, personality, difficulty, state, distance (m),
  closing speed (m/s), retained charge count, and defense side.

## Validation record

Runtime source revision tested in Windows:
`7949ef4a8291306a2ef09e8a364dc7643b8f4bf0` (Unity 6000.4.4f1, Windows x64).
Gameplay assembly SHA256:
`8960D0D9E5FC29B4C74AF4BBE24FE9E99332EF9D0E6639CA1553F9E7D618D4C3`.
Final origin/test check: `7bc407a4f375f7e522736158049a33b458719096`, unchanged.

| Check | Observed result | Evidence |
|---|---|---|
| Complete EditMode suite | 43 passed, 0 failed | [JSON](Evidence/editmode-results.json) |
| Complete PlayMode suite | 48 passed, 0 failed | [JSON](Evidence/playmode-results.json) |
| MainArena integration | 4 combinations, 2 matches each; damage/restore and menu/pause gates passed | [Editor trace](Evidence/mainarena-smoke.txt) |
| Existing tactics | Pivot and driving-circle; all retreat/flank/wait phases; stop/restart/target loss passed | [JSON](Evidence/tactics-regression.json) |
| Existing shared melee combat | Player keyboard and bot each produce one shared profile hit; stationary/spinning opponents passed | [JSON](Evidence/combat-regression.json) |
| Existing motor lab | Front/side/behind, range hysteresis, lost target, corner x3, angled wall, range tuning and 540 s moving target passed | [Trace](Evidence/scenario-regression.txt) |
| Imported robot damage | 30 existing regional damage checks passed | [Trace](Evidence/scenario-regression.txt) |
| Windows MainArena | 4 combinations, 2 matches each; real player keyboard movement and combat damage required; exit code 0 | [Trace](Evidence/windows-smoke.txt), [video](Evidence/windows-matches.avi), [frame decisions](Evidence/windows-decisions.tsv) |

The full NUnit suites and legacy regressions ran on the bot/controller code in
`3076bebc37ab06cb83d7e871dcb6268a16bd1a96`. Later runtime commits only strengthen
the opt-in smoke harness (focus validation and offscreen recording), not bot behavior.
The initial Editor MainArena trace precedes the committed-maneuver priority fix;
the final Windows runs and tactics regressions include it.

Measured deterministic scenario (seed 123, 0.80 s): Easy sampled 2 decisions and
Hard sampled 5; final aim errors were +5.845 and -1.221 degrees respectively.
Identical stationary entry: Aggressive chose Following; Counter chose Disengaging.
Three distinct frontal charges inverted the preferred evasion side; one continuous
charge stayed one memory entry. Tests also verify dilution, expiry, six-entry bound,
pause preservation, target reset, and recovery priority.

The first unattended Windows run lacked player focus and is excluded as keyboard
evidence. The harness now requires observed player input/movement and damage to the
bot. Hidden-window swapchain captures were black and are excluded; delivered video
renders the actual arena camera offscreen. This changes verification only.

PR target is `test`. GitHub CLI is not authenticated and the browser access host is
unavailable in this session, so PR creation/open-PR inspection needs authentication.
