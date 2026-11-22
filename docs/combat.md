# Archived combat setup

These notes document the retired temple_v2 player. The current Temple_Final_Room player and controls are described in the [character guide](../Assets/Character/README.md).

Player receives PistolCombat and CombatHealth automatically. CombatHealth adds
ProceduralHitReaction: a brief chest tilt backward/left after nonlethal damage.
No Hit triggers/clips are needed. Humanoid chest falls back to spine; for other
skeletons assign Chest manually. Death stops the reaction and disables player aim rig.

## Controls
RMB aims, LMB fires once per click. Magazine: 8; total starting ammo: 24 (8 loaded + 16 reserve); damage: 25; HP: 100.
R while aiming reloads in 2 seconds, transferring only missing rounds from reserve.
Reload is unavailable when reserve is empty. Hits, death, disabling the
component or leaving aim cancel reload without granting ammo. No animation events.

## Reload (manual Animator setup)
Create Bool IsReload (false). Create ReloadUpperBody Override layer, weight 1.
Assign a Humanoid Avatar Mask enabling body/head/arms/hands, excluding root/legs
and foot IK. Default state Empty has no motion. Add Hit&Death/Reloading.fbx as Reload.
Set Reload state Tag to Reload (NOT CombatBlock). Empty -> Reload: IsReload true,
no exit time, duration 0.1 seconds. Reload -> Empty: IsReload false, no exit time,
duration 0.1 seconds. Fixed Duration on, offsets 0. Clip Loop Time off.
Match playback to reloadDuration (default 2 s): state Speed = clip seconds / 2.
This tag suppresses aim rig and firing without blocking locomotion. Base Layer
remains PistolLocomotion, supplying leg animation. Remove obsolete competing reload
states/layers. Aim must remain held during reload in the current prototype.

## Death (manual Animator setup)
Create Trigger Die. Put the death state in Base Layer, tag CombatBlock.
Any State -> die: trigger Die, no exit time, no self transition, Fixed Duration
0.1 s, offset 0. No outgoing transitions; clip Loop Time off. HP zero stops input
but preserves gravity. Code disables aim rig and sets all upper-layer weights to zero.
Keep vertical fall in the pose (Bake Into Pose Y) with root motion disabled.
See [Ivan's integration guide](../Assets/Character/README.md) for the complete developer API and lifecycle.

## NPCs and effects
Add CombatHealth to NPC root and non-trigger colliders on its body. IDamageable is
resolved from hit collider parents. NPCs may implement IDamageable instead; avoid
multiple receivers on one actor. Use onHit/onDeath events to connect AI logic.
Player movement stops automatically; NPC AI must stop through onDeath.
Tracer renders for one frame; impact is a temporary red ring. Assign Effect Material
for builds to avoid stripping the fallback Sprites/Default shader. The crosshair uses
OnGUI. The Canvas HUD reads health and ammunition through GameHudBinding.

## Verify in Play Mode
Fire 8 shots; reload twice to expend all 24 rounds. Shot 25 must not fire. Reload while walking: legs continue, arms reload,
magazine refills to at most 8 after 2 seconds, consuming reserve. Release aim or take damage mid-reload: no refill.
Apply damage standing, crouching and aiming: upper body recoils, root stays grounded.
Four 25-damage hits kill a 100-HP target; subsequent hits do not repeat death events.

Review fixes: crouch resizes the capsule and checks standing clearance; aiming cannot
force standing through a ceiling. Muzzle overlap/body-to-muzzle checks reject obstructed
shots. Shot evaluation runs after the final procedural pose. Disabled cameras do not
modify FOV through aim-ray queries. Reload requires the saved Animator Bool IsReload.
