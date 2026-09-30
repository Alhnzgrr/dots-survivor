# ECS 3D Survivor

A survivor-like built on **Unity DOTS** (Entities 1.4) as a study of how the data-oriented
stack actually behaves when the entity count climbs: Burst-compiled systems, parallel
`IJobEntity` scheduling, and a spatial hash standing in for the broadphase a survivor-like
would otherwise not have.

Waves of enemies close in on the player, the weapon fires itself at whatever is nearest,
and the wave advances once the last of its enemies is down.

---

## What this is for

The interesting part of a survivor-like is not the loop, it is the population. A thousand
enemies all steering toward one point is the smallest honest test of whether a codebase is
data-oriented or merely written in ECS syntax — and the two are easy to confuse.

This project is the second pass over that question. The first pass ran, and was wrong in
the way ECS projects usually are: correct behaviour, main-thread systems, and a nearest-enemy
search that walked every enemy in the world once per shot.

| | Before | Now |
| --- | --- | --- |
| Burst | 1 system of 5 | Every system that can be |
| Threading | All main-thread `foreach` | `ScheduleParallel` on the two populations that scale |
| Nearest-enemy search | Full scan per shot — O(n) | Spatial hash, cells within range only |
| Spawn separation test | Full scan × 6 attempts per spawn | Same grid, one cell neighbourhood |
| Wave "is it clear?" check | Full scan per spawner per frame | One pass per frame, bucketed by spawner |
| Damage | Written straight into `Health` | Queued as events, applied once, on one thread |

---

## The spatial hash

`SpatialHashSystem` rebuilds a `NativeParallelMultiHashMap<int, SpatialEntry>` every frame,
in parallel, keyed by the enemy's grid cell. A rebuild beats incremental maintenance here:
the enemies all move every frame anyway, so tracking cell transitions would cost more than
refilling, and a rebuilt grid cannot drift out of sync with the world.

Queries then visit only the cells a radius actually touches:

```csharp
SpatialHashQuery.TryFindNearest(hash.Map, hash.CellSize, origin, weapon.Range,
                                out Entity target, out float3 targetPosition);
```

Cost scales with local density rather than total enemy count. Cell size is the one dial that
matters, and it is a real trade-off — too small and a query walks many nearly empty cells,
too large and the scan inside each cell costs what the grid was meant to save. It is exposed
on `GameSettingsAuthoring`.

The same pass also fills an `Entity → float3` map. That is not redundancy: a homing projectile
needs its target's position while writing its own transform, and reaching for a
`ComponentLookup<LocalTransform>` would alias the transform its own query already holds for
writing, which the job safety system rejects. Reading a position the grid pass already
gathered sidesteps the conflict rather than suppressing it — and a key that is missing
doubles as *this target no longer exists*.

> **Measurements.** The table above states complexity, not benchmarks. Numbers from the Unity
> Profiler on a specific machine and enemy count belong here; they have not been taken yet.

---

## Damage as events

Two projectiles landing on one enemy in the same frame is the normal case, not the edge case.
A parallel job cannot read-modify-write a shared `Health` component without losing one of
them, so producers never touch health:

```
ProjectileJob (parallel)  ─┐
                           ├─→ NativeQueue<DamageEvent> ─→ ApplyDamageSystem ─→ DeathSystem
EnemyContactDamageSystem  ─┘                                (single-threaded drain)
```

Death is its own pass so that anything wanting to react to it — a score tick, a pickup drop,
a sound — has one place to hook in, and so an entity is never destroyed halfway through the
frame that damaged it. `Dead` is an `IEnableableComponent`, so marking a death costs no
structural change.

---

## Frame order

Systems declare themselves into one of five groups rather than pairing off with
`UpdateBefore`/`UpdateAfter`, so adding a system cannot silently reorder an unrelated pair:

```
InputSystemGroup       →  reads devices
MovementSystemGroup    →  player and enemies move
SpatialSystemGroup     →  grid rebuilt from where everything now is
CombatSystemGroup      →  shooting, projectiles, contact damage, spawning
ResolutionSystemGroup  →  damage applied, deaths resolved, i-frames tick
```

The grid sits between movement and everything that queries it, so no system ever reads a
stale cell.

---

## Layout

```
Assets/GameFolders/Scripts/
  Core/      Health, damage events, the spatial hash and the group definitions
  Player/    Input (Input System, no generated actions asset), movement, stats
  Enemy/     Waves, follow steering, contact damage
  Combat/    Weapon, homing projectiles
```

Each area keeps `Components/`, `Systems/`, `Authorings/` and `Data/` apart, and every tunable
value lives on a ScriptableObject (`PlayerData`, `EnemyData`, `EnemySpawnData`) rather than in
a script.

---

## Running it

1. Open with **Unity 6000.0.x** with the Entities 1.4 package set already in `Packages/`.
2. Open the scene under `Assets/GameFolders/Scenes/`.
3. Put `GameSettingsAuthoring` on any object in the scene (cell size defaults to 4).
4. Press Play. WASD or arrows to move, or a gamepad's left stick. The weapon fires itself.

Enemy and player prefabs need `EnemyAuthoring` / `PlayerAuthoring` with their ScriptableObject
assigned, and the spawner needs `EnemySpawnerAuthoring` with a wave list.

---

## Scope

This is a study project, not a game. There is no UI, no progression between runs, no audio,
and the art is untextured primitives — the Asset Store prototype materials the first pass used
were removed, since redistributing them would breach the Asset Store EULA.

What it is meant to show is the part that transfers: how to keep a DOTS project data-oriented
once the entity count stops being small.
