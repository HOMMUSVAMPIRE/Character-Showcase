# Movement Abilities
The movement abilities on the ground and against walls (climbing, ladders, pushing and pulling, ropes, ziplines and bounce pads are in [traversal.md](traversal.md)), on the BasicMovementMode asset (Assets/ScriptableObjects/BasicMovementMode.asset). Each is a component of it ([Movement Components](movement.md#movement-components)), in Assets/Scripts/MovementModes/Components, with its settings under its name in the asset's Components list: Crouching (crouching and sliding), Rolling, Wall Running, Wall Sliding, Ledges and Vaulting. The settings named below are on that asset unless another component is named.

## Crouching, Sliding and Rolling
Press Crouch (C, or the east face button on a gamepad) to crouch, and again to stand: the character's collider lowers to Crouch Height (a share of its standing height) and it moves at Crouch Speed. It stands only where there's room: under something low it stays crouched, and stands by itself as soon as there's room again (a second press while it waits cancels that). Jumping from a crouch stands up and jumps, where there's room. Hold To Crouch makes Crouch a held button instead.

Pressing Crouch while sprinting (moving at least Slide Min Start Speed) slides. The character keeps the sprint's momentum (at least Slide Start Speed), low enough to pass under things it can't stand under, slows down at Slide Deceleration, and can be steered a little (Slide Steering). The slide ends in a crouch once it slows to Slide End Speed, after Slide Max Duration, or against a wall. Pressing Crouch again stands up out of it; jumping jumps out of it keeping its speed; sliding off an edge carries its momentum into the air.

Press Roll (Left Ctrl, or the left bumper) on the ground to roll: a quick, low, committed roll in the movement direction (or straight ahead), at Roll Speed scaled by Roll Speed Curve over Roll Duration. A roll can't be steered, jumped out of or dashed out of. It has a short cooldown and input buffer (Roll Cooldown, Roll Input Buffer), and there's one roll per press.

Landing from a high enough fall while moving, the character rolls by itself on impact, the way it's going, whatever the cooldown. The fall has to be at least Landing Roll Min Fall (a share of the character's standing height), and the character moving at least Landing Roll Min Speed, or steering; a fall of 0 turns it off. A jump pressed for the landing jumps instead.

The fall is measured from where it started: the last moment the character wasn't falling (moving down slower than `FallStartSpeed` in Components/Rolling.cs: the top of a jump, or a dash, which holds it level), or was on the ground or held by a wall or ledge (a wall run, climb or slide, a ledge, a vault). So dropping from a ledge counts from the ledge, a dash or a jump just before landing starts the fall again from there, and an ordinary jump on flat ground isn't high enough.

A roll ends standing, or crouched where there's no room, or if it was rolled from a crouch (its animation then blends into the crouch over its end, as a standing roll's blends into walking, rather than standing up first). Rolling with movement input held hands straight over to walking or sprinting as the roll ends: the character moves on at its walking speed (keeping only whatever speed the roll has beyond that), and the animation blends from the roll into the stride over the roll's end (from `RollBlendOutAt` in MannequinGenerator.cs), rather than stopping and starting again.

Crouching and sliding are one component, Crouching, whose Sliding Enabled toggle turns sliding off (pressing Crouch while sprinting then just crouches); rolling is the Rolling component. A Slide Max Duration or Roll Duration of 0 turns that ability off too, and turning Crouching off leaves the character unable to crouch or slide. The crouch height is the character's posture, which the mode sets: rolls go low to Crouching's Crouch Height even with Crouching turned off.

## Wall-Running
Moving fast along a wall beside the character in mid-air, high enough above the ground, while steering along it, runs along the wall. Its conditions:

- moving along the wall at least Wall Run Min Speed;
- at least Wall Run Min Height above the ground;
- steering within Wall Run Max Steer Angle of parallel with the wall (a wall run is asked for, not fallen into);
- the wall reaching down to the character's feet (to Wall Run Foot Height above them, a share of its height), within Wall Run Check Distance beside it and leaning no more than Wall Run Max Wall Tilt.

The character then runs at Wall Run Speed, facing along the wall, falling slowly (at Wall Run Gravity instead of gravity). A rising jump is cut down to Wall Run Max Rise Speed, and a fall caught. Running on a wall counts as touching the ground: it gives back the character's jumps and air dashes.

The run ends after Wall Run Max Duration, when the wall ends, when something slows it, on pressing Crouch, or on landing. The wall ends where its feet drop below the wall's bottom edge, or at its end: a wall turning more sharply than `WallRunMinCurveRadius` (Components/WallRunning.cs) is an edge or corner, not a curve to follow round. Steering off it (no longer along the wall, or away from it) only lets go once it has lasted Wall Run Release Delay, so turning toward the next wall to jump to doesn't drop the character off this one.

Jumping jumps off the wall, away from it (Wall Jump Away Speed) and upward (Wall Jump Up Speed), keeping the run's speed and giving back the character's air jumps. Air steering then comes back gradually, easing in from none over Wall Jump Steering Recovery (in the asset's Air Steering section, as it's the same after any jump off a wall), so the jump carries the character off the wall rather than being steered straight back onto it.

The same wall (one facing within `SameWallAngle` of it, in MovementHelpers.cs) can't be run along twice without touching the ground, but another can, after a short cooldown (Wall Run Cooldown), so wall jumps chain between facing walls. Everything else is in the BasicMovementMode asset's Wall Running component; turning it off, or a Wall Run Max Duration of 0, turns wall-running off.

## Wall Sliding
Hitting a wall head-on in mid-air (within Wall Slide Max Angle of facing it, at least Wall Slide Min Height above the ground) slides slowly down it. What counts is how the character hits the wall: moving into it at Wall Slide Min Approach Speed or more, whatever the steering. The speed it hit with is remembered for `WallSlideApproachMemory` (Components/WallSliding.cs), as the wall stops it at once. Only a character barely moving needs to steer into it.

The slide starts falling, or rising slower than Wall Slide Max Start Rise Speed (a fast rise carries on up the wall and is caught near its top, rising on into the slide under gravity). The character is pressed against the wall facing it, and its fall is braked (Wall Slide Braking) to Wall Slide Speed.

Jumping kicks off the wall, upward (Wall Slide Jump Up Speed) and away from it (Wall Slide Jump Away Speed), and air steering comes back gradually after it, as after a wall run's jump. It gives back the character's air jumps and air dashes, but only off a different wall from the last one jumped off since landing. So kicking between facing walls climbs them (a narrow chimney, kicking at each wall in turn), with no limit to the kicks between different walls. Off the same wall there are only Wall Slide Max Jumps kicks in a row before landing, and hopping back up it needs the character's own jumps or dashes each time, so a wall can't be climbed in hops alone.

Steering away lets go once it has lasted Wall Slide Release Delay (not for a moment, so aiming a jump doesn't drop the character off). Landing, the wall ending, Crouch, or sliding for Wall Slide Max Duration end the slide; after dropping off with Crouch or sliding off the end of its time, that wall can't be slid down again before landing. Glancing along a wall instead wall-runs.

Only a wall up against the character's body is slid down: one reaching `WallSlideBodyReachShare` of the way into the ledge-grab window (between Ledge Grab Min Height and Ledge Grab Max Height) above its feet. The top edge of a lower wall, caught with the legs, is vaulted over instead (see [Vaulting](#vaulting)), or fallen past until it can be grabbed; and a ledge within reach is still grabbed out of a slide.

The BasicMovementMode asset's Wall Sliding component tunes it (a wall is as upright as Wall Running's Wall Run Max Wall Tilt allows, and reaches up the body by Ledges' grab heights, whether or not those components are turned on); turning it off, or a Wall Slide Max Duration of 0, turns wall sliding off. The mannequin's own wall-slide pose plays (hands spread on the wall at head height, one knee raised against it), and WallSlideIK puts the body against the wall and the hands flat on it.

## Ledges
In mid-air, heading toward a wall (within Ledge Max Grab Angle of head-on, its face within Ledge Grab Reach) whose top is within reach of the character's hands (between Ledge Grab Min Height and Ledge Grab Max Height above its feet, shares of its height) with room to hang below it, the character catches hold of the ledge. That counts as touching the ground: it gives back the character's jumps and air dashes. It snaps into hanging from it (at Ledge Snap Speed): straight below the point of the edge it grabbed, its feet Ledge Hang Height (a share of its height) below the ledge, against the wall, facing it, without gravity.

LedgeHandIK, on every model, places its hands directly on the ledge: the wrists on its top just back from the edge, spaced along it by its Hand Spacing, palms down with the fingers over the edge. It moves the body flush against the wall and to the height that lets the hands reach (the collider stays its radius from the wall; the model doesn't). It eases in on catching hold, and the hands let go partway up a climb (LedgeHandIK's Climb Release At).

A jump passes ledges on the way up and catches them near its top or falling: they're only grabbed rising slower than Ledge Max Grab Rise Speed. Hanging, Jump climbs up onto the ledge (rising up the wall, then forward onto it, over Ledge Climb Duration), crouched if there's only room to crouch on top, and not at all if there isn't room for that. Where it stands on top is found by lowering it onto whatever is there, so a ledge at the foot of rising stairs climbs up onto the stairs.

Crouch, or steering away from the wall, lets go, and the same ledge can't be caught again for a moment (Ledge Regrab Delay). Ledges are kept in the space of what they belong to, so the character hangs from and climbs ledges that move. The BasicMovementMode asset's Ledges component tunes them; turning it off, or a Ledge Climb Duration of 0, turns ledges off (and climbing over the top of a climbable surface with them).

## Vaulting
Heading into a wall whose top is too low to grab as a ledge but too high to step onto (above the step height, the MovementController's Max Step Height on the Character prefab, and below Ledge Grab Min Height), with room to go over it, the character vaults it:

- in mid-air, steering into it or moving toward it within Ledge Max Grab Angle of head-on, and, like ledges, rising slower than Ledge Max Grab Rise Speed, so a jump that's still climbing clears the wall itself;
- on the ground (with Vault From Ground on), sprinting into it within Vault Ground Max Angle of head-on; walking into it, or running along it, doesn't. From the ground it only vaults over to somewhere to land no more than Vault Max Drop (a share of the character's height) below its feet, so a railing at the edge of a drop stays a railing.

Anything with something to stand on just in front of it, no more than a step below its top, is a step, not a wall: stairs rising ahead are climbed, never vaulted.

Planting a hand on the top counts as touching the ground: a vault (or a step up) gives back the character's jumps and air dashes. A vault spends Vault Rise Duration getting a hand onto the top and lifting the body up to it while closing in on the wall. Then it swings over the edge, speeding up evenly to the speed it leaves with, until the character's whole body is past the wall's face; then it carries on through the air (over something thin) or along the top (onto something deep). It leaves at Vault Speed Retention of the speed it came in at, and at least Vault Min Speed.

Onto the top of something deep enough to stand on and no higher than Vault Step Up Height (a share of the character's height) above its feet, it steps up instead, as onto stairs: lifted onto it as fast as it takes to get there (up to `StepUpRiseSpeed` in Components/Vaulting.cs), keeping its speed, with no vault.

The quick rise is for feel; to look right, the model doesn't pop up with the collider. The AnimationController holds it where it was and eases it up after the character (its Step Up Smooth Time, never more than Step Up Max Lag behind), and foot placement tucks the feet up onto the top meanwhile (any foot over it, or within FootPlacementIK's Step Up Tuck Reach of its edge, is lifted to stand on it), so the knees come up and straighten as the body catches up. The AnimationController's Stepping Up settings and FootPlacementIK's tune it.

The wall is looked for far enough ahead that a fast approach is caught before it reaches it, and a vault over something moving goes over it. The BasicMovementMode asset's Vaulting component tunes it (what's too low to grab is below Ledges' grab heights, whether or not ledges are turned on); turning it off, or a Vault Rise Duration of 0, turns vaulting off.

The Quaternius library's safety vault plays over a vault. The VaultProgress parameter drives its time: the part of the clip where it plants its hand and lifts its body (up to `VaultState.RiseShare` in MovementAbilityStates.cs) over the rise, and the rest over the swing over. It's imported with its own hop taken out of its pose (Prepare Quaternius Animations, see [Quaternius animations](animation.md#quaternius-animations)), so the body rises as the character does rather than that much higher. LedgeHandIK plants its right hand on the top through the middle of the vault, just back from the edge (its Vault Hand Placement and Vault Planted Between), and lowers the body so the hand reaches it, so it presses itself over the top rather than floating clear of it on its collider.
