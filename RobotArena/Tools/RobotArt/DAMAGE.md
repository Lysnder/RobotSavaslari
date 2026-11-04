# Robot damage prototype

`RobotDamageVisuals` partitions each current readable, single-material robot
mesh at startup into body, four wheels, weapon and four armor regions. It keeps
every original triangle, UV, tangent and normal. Generated meshes share the
original material/textures; no extra GLB or texture download is introduced.
The cut volumes in `Classify` are specific to the current red/blue models.
Review them when replacing model geometry. This is prepared regional breakage,
not arbitrary fracture or real-time metal deformation.

Contact damage carries the contact position, surface normal, impulse and weapon
flag into `RobotHealth`. Fast body impacts scale with relative speed; existing
weapon damage and shared 0.6s contact cooldown remain. Contacts with the floor,
walls and debris do not count as opponent weapon hits.

Default regional durability (Inspector fields on `RobotDamageVisuals`):

| Part | Durability | Result |
| --- | ---: | --- |
| Armor region | 22 | Detaches; exposes a simplified inner chassis |
| Wheel | 30 | Detaches; -18% top speed/traction, -12% turning, mild steering pull |
| Weapon | 45 | Detaches; weapon attacks and animation stop |
| Body | Does not detach | Keeps the main robot stable until total health reaches zero |

Regional hits still reduce the existing 100-point total health. Repeated hits
to one part add wear, then detach it; spread-out hits can defeat the robot before
any single part fails. Procedural scratches, chipped metal and scorch marks
use four recent contact marks per region. Shared source materials are untouched.
Impacts emit sparks, dust and a synthesized metallic sound. Below 35% health,
the robot emits smoke.

Each part can detach only once per round: at most 9 debris pieces per robot.
Debris uses box colliders rather than expensive high-poly mesh colliders, ignores
its owner, inherits its velocity and expires after 12 seconds. Per-robot particle
budgets are 128 sparks and 64 smoke/dust particles. Generated meshes, materials
and audio are released on destruction. Match restart restores health, all parts,
wear, handling and weapon operation, and clears particles and debris.

The shaders live in `Assets/Resources/` so runtime-created materials also work
in player builds. No additional package is required.

Verification: in Play Mode, evaluate
`RobotSavaslari.Editor.RobotDamageChecks.RunInPlayMode()` through Unity MCP.
It runs 30 assertions on the real imported robots covering triangle preservation,
regional wear, repeated damage, physics debris, mobility/weapon loss, restore,
invalid damage and defeat. It leaves both robots healthy and stopped for
inspection. Call `ArenaMatchController.RestartMatch()` to resume normal combat.
Also verify real body contacts and inspect the Game view: direct damage checks
alone do not prove the physics callback path.
