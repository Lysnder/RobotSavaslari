# Add opponent personalities, threat avoidance and tactical memory

Target: `test` ← `feature/opponent-behaviors`.

The existing bot now supports Aggressive and Counter personalities independently
of Easy/Hard difficulty. Ready offers all four combinations and applies the
selection to the real bot after ConfigureCombat creates it. The bot reacts to
relative Rigidbody motion, chooses physics-checked escape points, changes tactics
using actual health/part loss, and remembers up to six distinct recent charges.
Pausing preserves memory; new matches and invalid targets clear it.

Health, weapon damage and motor capability remain unchanged. Existing recovery,
committed reposition cycles and shared melee intent retain priority and gating.
MainArena, prefabs, motors, damage sources and match/settings controllers are unchanged.

## Validation

- Full EditMode: 43/43; full PlayMode: 48/48, including 23 new bot cases.
- Existing scenario checks: approach/range/target loss, corners, angled wall and
  540 simulated seconds against a moving target passed.
- Existing tactical, shared melee and 30 regional damage checks passed.
- MainArena: all four combinations, two matches each.
- Windows x64: all four combinations, two matches each; actual keyboard movement,
  player-to-bot damage, bot movement and melee intent, single result, clean second
  match, real weapon/two-wheel loss and restore, menu/settings gates and pause passed.
  Player exited 0.
- Seed 123 over 0.8 s: Easy sampled 2 decisions, Hard sampled 5; aim errors
  +5.845° and -1.221°. Same initial entry: Following versus Disengaging.

[Delivery report and profile table](https://github.com/Lysnder/RobotSavaslari/blob/feature/opponent-behaviors/RobotArena/Documentation/Bots/Opponent-Behaviors.md),
[Windows result log](https://github.com/Lysnder/RobotSavaslari/blob/feature/opponent-behaviors/RobotArena/Documentation/Bots/Evidence/windows-smoke.txt),
[Windows video](https://github.com/Lysnder/RobotSavaslari/blob/feature/opponent-behaviors/RobotArena/Documentation/Bots/Evidence/windows-matches.avi),
[video frame decision trace](https://github.com/Lysnder/RobotSavaslari/blob/feature/opponent-behaviors/RobotArena/Documentation/Bots/Evidence/windows-decisions.tsv),
[Ready selection](https://github.com/Lysnder/RobotSavaslari/blob/feature/opponent-behaviors/RobotArena/Documentation/Bots/Evidence/ready.png).
Video is the actual arena camera rendered offscreen at nominal 5 fps, without HUD/audio.

## Revisions

- Branch base main: `8452fa4a9f568966c98f5a2468ba57436ee05624`.
- Initial and final observed test: `7bc407a4f375f7e522736158049a33b458719096`.
- NUnit/legacy regression behavior source: `3076bebc37ab06cb83d7e871dcb6268a16bd1a96`.
- Windows tested runtime: `7949ef4a8291306a2ef09e8a364dc7643b8f4bf0`.
- Later commits only adjust the opt-in smoke harness and attach evidence;
  the bot/controller code is unchanged after its full suite.

The user requested a main-based branch; main and test had identical content at
creation. No other feature branch is required.
