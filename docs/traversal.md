# Climbing and Traversal
Climbing walls and ladders, pushing and pulling objects, ropes, ziplines, bounce pads, swimming and wading. Each is a component of the StandardMovementMode asset (Assets/MeridianMotion/Content/Movement/StandardMovementMode.asset; see [Movement Components](movement.md#movement-components)), in Assets/MeridianMotion/Runtime/Characters/MovementModes/Components, with its settings under its name in the asset's Components list. The settings named below are on that asset unless another component is named.

*See also:* [Troubleshooting: the body and the ground](troubleshooting.md#the-body-and-the-ground), [Why the level elements are their own assembly](decisions.md#the-level-elements-are-their-own-assembly-and-the-builders-attach-them-november-2026).

What these are used on (climbable surfaces, ladders, pushable objects, ropes, ziplines, bounce pads, water, moving platforms) are level elements: components in Assets/MeridianMotion/Runtime/LevelElements (namespace `MeridianMotion.Levels`, the `MeridianMotion.Levels` assembly), which know nothing about the character. A level gets one by adding its component to an object (Add Component > Rope, Water, …), or by naming an object as a fixture, "Name | key=value | …", which `LevelFixtureSetup` (in the level elements' editor assembly) turns into the element. Every level build runs it before saving, and Set Up Scene runs it on the scene it sets up, leaving what's set up already as it is. The fixtures' names are in [Level fixtures](#level-fixtures), below, and adding a new kind in [Adding Level Content](showcase/adding-level-content.md#a-new-level-mechanic-a-fixture). Where the showcase levels have each, and how they look, is in [Mechanics in the showcase levels](showcase/levels.md#mechanics-in-the-showcase-levels).

## Level fixtures
A fixture is an object of a level named for the level element it stands for, with its settings after the name: a level can be built (or modelled) without any code of the character's or the level elements', and set up afterwards. The showcase's level builders build every mechanic this way. `LevelFixtureSetup` (in the level elements' editor assembly, Assets/MeridianMotion/Editor/LevelElements) turns each into the level element it stands for. Every build runs it before saving the scene, so a built level's mechanics work as it is; Set Up Scene runs it again, leaving what's set up as it is, for a scene built some other way. The names are the level elements' convention, which any scene can use:
- "Moving Platform (…) | travel=x,y,z | period=s | pause=s | spin=x,y,z | phase=f | smooth=0/1" gets a kinematic Rigidbody and a TickedPlatform with those settings;
- "Pushable Crate | pull=0/1 | speed=f" gets a Rigidbody and a PushableBlock (Can Be Pulled, Speed Multiplier);
- "Ladder | height=m | width=m | spacing=m | rail=m | rung=m" gets a Ladder with those settings, which keeps the level's mesh ("Ladder (future): …" markers aren't ladders);
- "Rope | length=m | thickness=m" (at its anchor) gets a Rope, which draws itself as it swings, in the level's material ("Rope Anchor" placeholders and "Rope Swing (future)" markers aren't ropes);
- "Zipline | length=m | speed=m/s" (at the cable's start, its +Z along it) gets a Zipline that keeps the level's cable;
- "Bounce Pad | height=m | keep=f" gets a LaunchPad, its "Spring" child squashing on each launch;
- "Water | size=x,y,z | label=name" (at its surface's centre) gets a Water of that width, depth and length that keeps the level's surface;
- "Climbable Panel | speed=f" gets a ClimbableSurface (the whole panel climbable, at that speed multiplier); only the panel is climbable, not the wall it stands on, so a climb stops at its edges;
- "Timing Gate | course=name | role=start-finish/start/finish | distance=m" (a trigger) gets a TimingGate, and each course a CourseTimer, the scene's own or one made in a Course Timers group at the scene's root (`LevelFixtureSetup.CoursesName`), which Set Up Scene has the spawner tell who's racing; a gate's display, "Timing Display | course=name", shows its course's time under way, the last and best, and (with a distance) the speed.

A CourseTimer times laps through one start-and-finish gate, or runs from a start gate to a finish gate, in game time. A pass too soon after a lap starts (the CourseTimer's Minimum Run Time) is ignored. Only its racer counts once it has one; until then anything passing with a Rigidbody does. The fixtures can also be added and named by hand in any scene that's then set up. Spawn markers, objects named "Spawn Point …" (`LevelFixtureSetup.SpawnMarkerPrefix`), aren't level elements: Set Up Scene makes them the spawner's spawn points (see [Spawning the Character](spawning.md#setting-up-a-scene)).

## Wall Climbing
Surfaces with a ClimbableSurface component (on a collider, or on a parent to make a whole group climbable) can be climbed, and so can the parts of an object painted climbable with a PaintedClimbableSurface (see [Painting climbable areas](#painting-climbable-areas)); nothing else can. A climbable wall is climbed instead of wall-run along or slid down.

**Taking hold.** Touching a climbable surface while heading anywhere remotely into it, on the ground or in mid-air, takes hold of it: steering into it, or moving into it at least Wall Climb Min Approach Speed, within Wall Climb Start Max Angle of head-on (anything but sliding along it), and within Wall Climb Start Reach of it. Holding onto a climbable surface counts as touching the ground: taking hold gives back the character's jumps and air dashes.

**Moving on it.** Climbing, the character faces the surface without gravity and holds still without input. The stick moves it over the surface at Wall Climb Speed (times the surface's own Speed Multiplier): up and down with the stick's up and down, sideways with left and right, as seen along the input's look (the player's camera, for the player: see [Input](movement.md#input)), so up on the stick climbs up whichever side of the character the camera is on. Wall Climb Snap Speed draws it in against the surface, so it stays against it as the surface curves.

**The climbable area holds the character in.** What has to stay on it is the character's grip, a point on its body Wall Climb Grip Height (a share of its height) above its feet. The character climbs on as long as the grip stays within `WallClimbEdgeTolerance` (in Components/WallClimbing.cs) of a climbable surface, and stops where it wouldn't: a move that would take it off is cut down to whatever part of it (up, down or sideways) stays on.

It goes round outside corners onto the next climbable face, its grip swinging round the edge, and into inside corners onto the climbable wall it meets, so climbable surfaces that join are climbed as one. Where something unclimbable comes next instead (the plain wall round a climbable panel set on it, or a ledge that isn't climbable), it stops at the edge.

**Climbing over the top.** Pushing up with a walkable top within reach of its hands (the window a ledge is grabbed in, Ledge Grab Min Height to Ledge Grab Max Height: see [Ledges](abilities.md#ledges)), it climbs up onto the top as a ledge climb does from hanging, and hangs from the edge if there's no room up there. A top it climbs past before reaching one (leaning back further than Wall Climb Max Lean Back from upright, or past the top of the climbable area) is climbed onto from there. Surfaces leaning out over the character by up to Wall Climb Max Overhang can be climbed too.

**Stepping on and off.** Taking hold from standing on the ground, the character first steps up onto the surface, holding still meanwhile, for Wall Climb Enter Duration. Pressing down at the bottom with the feet on the ground steps back off it onto the ground the same way, for Wall Climb Exit Duration (0 for either does it at once). Taking hold in mid-air, letting go and jumping off don't step.

**Letting go.** Jump kicks off the surface, upward (Wall Climb Jump Up Speed) and away (Wall Climb Jump Away Speed), giving back air jumps as a wall jump does ([Wall-Running](abilities.md#wall-running)). Crouch lets go and drops. After letting go, nothing can be climbed for Wall Climb Regrab Delay.

The character climbs on surfaces that move, carried with them. The StandardMovementMode asset's Wall Climbing component tunes it all; turning it off, or a Wall Climb Speed of 0, turns climbing off. Climbing over the top of a surface is a ledge climb, so it needs Ledges turned on.

### Painting climbable areas
To make only part of an object climbable (one face of a box, a strip up a wall, a patch of handholds), add a PaintedClimbableSurface to it and paint the climbable regions in the Scene view:

- With the object selected, press Paint in the component's inspector, or pick Paint Climbable Surface in the Scene view's Tools overlay. It's an Editor Tool, so while it's on, dragging paints instead of selecting.
- Drag over the object's colliders (its own and its children's) to paint, and Shift-drag to erase; [ and ] size the brush.
- The paint shows as a green overlay whenever the object is selected, and strokes can be undone. Paint and Erase can also be called from code.

Paint is kept as cells in the object's own space, so it moves, turns and stretches with the object. The cells' size is the component's Cell Size setting, and changing it clears the paint. Each cell is painted for the face it was painted on, so painting the front of a wall doesn't paint its sides or back. The character climbs only where its grip is over paint, stopping at the paint's edges as at any other edge of the climbable area, and goes round a corner only where the face round it is painted too.

**Seeing the paint in the game.** The paint can be drawn by any material made to show it: one whose shader has a mask texture property named `_ClimbableMask` (`PaintedClimbableSurface.MaskPropertyName`), read in the mesh's UV2, white where the surface is climbable and black where it isn't. What the shader does with it is its own (the showcase levels' concrete blends in a climbable look: see [Mechanics in the showcase levels](showcase/levels.md#mechanics-in-the-showcase-levels)).

When a stroke's done, it paints a mask texture for each of the object's renderers with such a material, in the mesh's UV2, at the component's Mask Size resolution, and gives it to the renderer as per-renderer data, so the material stays shared. The masks are saved as assets in Assets/ClimbablePaint, a folder per scene. They're optional: turn Paint Masks off and the material shows none. Every unpainted object shows the material's own mask, so such a material should have a black one: Tools > Meridian Motion > Utilities > Set Up Climbable Materials gives any material with a climbable mask that has none ClimbableMask_None (Assets/MeridianMotion/Content/Materials), a single black pixel.

**The mesh needs a UV2** that gives each face its own space. The first UV set won't do: Unity's cube puts all six faces on the same square.

- On the first stroke, painting onto Unity's cube elsewhere swaps it for Cube (UV2) (in Assets/ClimbablePaint), and a mesh in the scene for a copy unwrapped by Unity.
- An imported model needs Generate Lightmap UVs on in its import settings, which the component's inspector offers to turn on (it changes the model for everything using it).

### Climbing animations
Climbing plays the Quaternius library's climbing loops (Universal Animation Library Pro: see [Quaternius animations](animation.md#quaternius-animations)), in a Wall Climb state entered while the WallClimbing parameter is set. The state blends the library's climbing idle and its climbs up, down, left and right by which way and how fast the character climbs: the ClimbRight and ClimbUp parameters, in m/s across and up the surface, at the authored size. Each climbing loop plays at the speed its hands are made for, and faster past it, however fast the climb is tuned (see [Walking and running](animation.md#walking-and-running)).

Each loop is placed in the blend at the speed its holding hands are made for, measured on the mannequin when it's generated (`MotionCaptureLocomotion.ClimbSpeed`, in Assets/MeridianMotion/Editor/Animation/MotionCaptureLocomotion.cs), and played faster above it, so the hands keep their holds at any climbing speed. WallContactIK moves the body in toward the surface until the nearer hand is on it (the clips are made for a wall nearer the body than the body's front (`FrontExtent`) lets it come), easing between holds; foot placement is off while climbing.

Stepping onto the surface from the ground and off it onto the ground play the library's climbing enter and exit (Climb Enter and Climb Exit states, timed by ClimbStepProgress, while ClimbEntering or ClimbExiting is set). Climbing up onto a ledge, from hanging or from climbing a wall, plays its ClimbLedge, which starts from a climbing pose (UAL2's ClimbUp_1m without the Pro library). Without the library, the mannequin's hang pose shows, with no enter or exit.

The MMotionAnimator passes climbing on to animation: CharacterAnimationState.wallClimbing, with the surface in wallPoint and wallNormal, and the climb's speeds in localVelocity (x to the right, y up).

## Ladders
A Ladder component makes a ladder: set its Height (and Width, Rung Spacing, rail and rung thickness) in the inspector, and with Build Mesh on it makes its own rails, rungs and box collider to match whenever they change (Add Component > Ladder on an empty object; give it a Material). Its origin is the middle of its foot, and it's climbed from its front, the side its +Z faces.

**Climbing it.** A ladder is climbed as a climbable surface is (see [Wall Climbing](#wall-climbing)), with the same settings, but only straight up and down it, on its middle, and only from its front:

- Heading into its front takes hold of it (from the ground, stepping onto it first). Up and down on the stick climb it, sideways does nothing, and the character is held on the ladder's middle, facing it.
- It climbs as far as the ladder's top and no further, and climbs off the top onto a floor there when its hands reach it (a ladder standing on its own isn't climbed over).
- Pressing down at the bottom steps off; Jump kicks off it and Crouch lets go.
- A ladder gives back jumps and air dashes as other climbing does, and moves the character with it if it moves.

**Animating it.** Climbing a ladder is animated procedurally. The Animator's Ladder state holds a climbing pose (the library's climbing idle), and LadderIK, on every model, puts the hands on the rungs (just in from the rails) and the feet on the rungs (or the ground), and draws the body in to the ladder.

Where each hand and foot is depends only on how high the character is up the ladder, so it's the same climbing up or down at any speed, and holding still holds every rung. Each limb holds a rung while the body climbs past it, then, over the last part of each two rungs' climb (LadderIK's Swing Share), moves up two rungs, out from the ladder and back, in diagonal pairs: the left hand with the right foot, then the right hand with the left. A hand holds rungs up to LadderIK's Hand Reach above the feet, and a foot stands on them up to its Foot Reach (at the authored size). It eases in over stepping on and out over stepping off. LadderIK's other settings tune the placement and swing.

(The CMU motion-capture set has two ladder climbs, in Assets/Showcase/Animation/CMU/Climb Hang, but they fit one ladder's rungs, which the procedural climb fits to any.)

## Pushing and Pulling
Large objects with a PushableBlock component (a kinematic Rigidbody with a BoxCollider) can be pushed and pulled.

**Taking hold.** Holding Interact (E, or the north face button) facing one of an object's sides, close to it (within Grab Reach, and within Grab Max Angle of head-on), takes hold of it. Steering toward it then pushes it, and away from it pulls it, at Push Pull Speed times the object's own Speed Multiplier (lower for heavier objects); sideways steering does nothing. Objects can be made push-only (Can Be Pulled, on the PushableBlock).

**Moving it.** An object's moves are swept against the world, so it stops at walls, and the character stops with it. Pushed off an edge, an object falls (it has simple gravity of its own: the PushableBlock's Gravity and Max Fall Speed) and the character lets go. Letting go of Interact or leaving the ground lets go too. Holding an object, the character faces it and can't jump, dash, crouch or roll. Without Interact held, an object is just an obstacle.

The StandardMovementMode asset's Pushing And Pulling component tunes the speed and reach; a Grab Reach of 0 turns pushing and pulling off.

## Ropes
A Rope component makes a rope to swing on: it hangs from its object's position (its anchor), Length metres long, and with Build Mesh on it draws itself (Add Component > Rope on an empty object where the rope is tied; give it a Material). Nothing collides with a rope: characters find it by where it is.

**Catching hold.** Reaching a rope in mid-air catches hold of it: it has to pass within Rope Grab Reach of the character's hands, which hold it Rope Grip Height (a share of the character's height) above its feet. The hands catch it where they are, but never nearer the anchor than Rope Min Grip Distance. Catching hold gives back jumps and air dashes.

**Swinging.** The character swings on the rope as a pendulum about the anchor, carried on by the speed it caught hold with: run and jump at a rope and the swing takes you on past it and up.

- Gravity swings it (the character's own, so a rope swings as quickly as the character falls).
- The stick pumps it at Rope Swing Acceleration, pushing the way it points: push with the swing to swing higher.
- It loses Rope Swing Damping (a share of its speed) every second, so without input it slowly dies away and never rises higher.
- It swings out no further than Rope Max Swing Angle from hanging straight down, and no faster than Rope Max Swing Speed.
- Something in the way stops the swing, the character staying on the rope. The character faces the way it steers.

**Letting go.** Jump lets go with the swing's speed and a jump up (Rope Jump Up Speed); Crouch, or touching the ground, lets go. Let go of, the rope swings on by itself, slowing by the Rope's own Swing Damping until it hangs straight again, and can be caught on its way back. The same rope can't be caught again for Rope Regrab Delay after letting go (another can, so the character can swing from rope to rope).

The rope is drawn between physics steps as the character is, so the hands stay on it. The StandardMovementMode asset's Rope Swinging component tunes it; turning it off, or a Rope Grab Reach of 0, turns rope swinging off.

## Ziplines
A Zipline component makes a zipline: a straight cable from its object's position to its End Point (in its own space), ridden at up to its Max Speed, which with Build Mesh on draws its own cable.

**Catching hold.** Reaching the cable in mid-air (it passing within Zipline Grab Reach of the character's hands: walking off a platform under it, say, or jumping up to it) catches hold of it. The character rides along it, hanging below it (its feet Zipline Hang Height, a share of its height, under the cable) and facing the way it rides. Catching hold gives back jumps and air dashes.

**The ride.** The ride starts at the character's speed along the cable, either way. Gravity along the cable speeds it up downhill and slows it uphill, and drag takes Zipline Drag (a share of its speed) every second. Downhill it goes at least Zipline Min Speed, so a gentle cable still gets the rider to its end (caught heading uphill, it slows, stops and comes back), and never faster than the zipline's Max Speed. At the end it's heading for, it lets go and carries on at the ride's speed.

**Letting go.** Jump lets go with a jump up (Zipline Jump Up Speed) on top of the ride's speed; Crouch, touching the ground, or being stopped by a wall or a ceiling lets go. The same zipline can't be caught again for Zipline Regrab Delay after letting go.

The StandardMovementMode asset's Ziplines component tunes it; turning it off, or a Zipline Grab Reach of 0, turns ziplines off.

## Bounce Pads
A BouncePad component on an object makes everything collidable on it a bounce pad. Standing on one (landing on it, or walking onto it) launches the character along the pad's up, fast enough that its feet rise the pad's Launch Height above it (worked out for the physics steps, so the height is exact), keeping the pad's Momentum Kept share of its speed across it (all of it carries a run across the pad on). Holding Jump as it comes down on the pad multiplies the height by Bounce Jump Boost. A tilted pad launches along its tilt.

The launch counts as the character's jump: its air jumps and dashes are left, and it ends whatever else the character was doing (a dash, roll or slide, or holding a rope or a zipline). A pad can squash its Spring (its springy top) and let it spring back on each launch, for show, and raises Launched for effects and sound.

The StandardMovementMode asset's Bounce Pads component holds Bounce Jump Boost and Bounce Height Multiplier, which every pad's height is multiplied by; turning it off, or a multiplier of 0, turns pads off.

## Swimming
A Water component makes a body of water: a box whose top, at its object's position, is the water's surface, reaching down and across its Size (width, depth and length, in its own space; it may be turned about the vertical, not tilted). Nothing collides with water: the character finds it as it finds ropes and ziplines, and stands on whatever is built under it (a pool's floor). With Build Mesh on, it draws its own flat surface in its Material.

**Getting in.** Where the water over the character's feet is at least Swim Depth deep (a share of its standing height), it swims: walking down into it, or falling or jumping in. Getting in gives back jumps and air dashes, as touching the ground does, and ends whatever else the character was doing (a dash, roll or slide). Shallower water is walked through.

**Floating.** The character swims at the surface, its feet hanging Swim Depth below it, floating up off a beach walked down into, or off the bottom. Falling in, it plunges deeper and floats back up: the water's drag takes Swim Plunge Drag (a share of its speed) for each metre it plunges, so a plunge from high goes only a little deeper than one from low, and it rises back no faster than Swim Surfacing Speed, settling at its depth without bobbing past it.

**Swimming.** The character swims the way the movement input points, relative to the input's look (the player's camera, for the player: see [Input](movement.md#input)), at Swim Speed (Swim Sprint Speed with Sprint held), facing the way it swims. Its speed changes at Swim Acceleration: speeding up, turning, slowing to a stop without input, and taking away the speed it falls in with. It can't dash; Crouch dives under the surface (see [Diving](#diving)).

**Getting out.** Jump jumps out, its feet rising Swim Jump Height above the surface: enough to climb out onto an edge a little above the water, or to catch a ledge. It's the character's jump from the ground, so its air jumps are left. Swimming into shallows, it finds its feet and walks out once it stands on a floor a little shallower than Swim Depth (so wading at the edge doesn't flicker in and out of swimming). Swimming into something whose top sticks out of the water no more than Swim Vault Height (a share of the standing height) vaults over or onto it, as a vault on land does: onto a pool's side, or over a low divider back into the water beyond (handed to Vaulting; a Swim Vault Height of 0 never vaults from the water). It's shown as climbing out (the ledge climb's animation, its hands on the edge), not as a vault. Swimming into a taller edge whose top is within reach of the character's hands (no higher above its feet than Ledges' Ledge Grab Max Height) climbs out onto it, as climbing over a ledge's top. A ladder or a climbable wall rising out of the water is climbed out too (Wall Climbing comes before Swimming in the components), straight over its top if that's within reach. A side higher than that is only jumped onto. Off the water's edge, it carries on as it was going.

The StandardMovementMode asset's Swimming component tunes it; turning it off, or a Swim Depth of 0, turns swimming off (the character sinks to the bottom and walks there). The swimming animation is floated at the default character's Swim Depth, again by itself when it's retuned (see [Swimming animations](animation.md#swimming)).

### Diving
At the surface, Crouch dives: the character is pushed down under the surface at Swim Dive Speed and swims underwater. Underwater it swims the way the movement input points relative to the input's look (the player's camera, for the player), up and down with it (looking down and pushing forward swims down), at Swim Speed (Swim Sprint Speed with Sprint held). Holding Jump rises and holding Crouch sinks, at Swim Vertical Speed, whichever way the input looks, so it can look at something while it goes up or down. With no input at all it drifts up at Swim Buoyancy; back up at its swimming depth (rising, or carried up a slope such as a beach), it's swimming at the surface again, and finds its feet in the shallows as a swimmer at the surface does (a press of Jump underwater only rises: it doesn't jump out on arriving). Speeds change at Swim Acceleration, and it faces the way it swims across.

Underwater its body lies flat along the way it swims, resting on its feet, so it swims along the floor and under what a swimmer would (an arch, say). It stands again at the surface. Lying, it turns only as far as it has room to (in a narrow channel, say: see [Collision](movement.md#collision)). Steered up or down, its body pitches along the way it swims, nose first, up to Swim Max Pitch (0 keeps it level), as far as it has room to; it levels out without steering, and doesn't pitch down into the pool's floor it's swimming along. The swimming animations float the body Swim Depth above the feet, as treading water at the surface needs, so underwater the model is drawn lowered to the body, easing down as it dives and back up as it surfaces over the Animation Controller's Model Drop Smooth Time. Diving raises DiveStarted, and DiveEnded when it's back at the surface or leaves the water. A Swim Dive Speed of 0 turns diving off. Underwater, the Animator plays a state of its own (Swim Underwater: see [Swimming animations](animation.md#swimming)).

There's no breath in this package: for a game's own (or for muffling sound, or an underwater look), the MMotionAnimator raises HeadSubmerged when the character's head goes under a water's surface and HeadSurfaced when it comes back above, and says whether it's under now (IsHeadUnderwater, HeadWater). The head is the model's head bone as it's posed each frame, or its eye point for a model without a humanoid head; it must go Head Water Margin (on the MMotionAnimator) past the surface either way to count, so a head bobbing at the water line (the swim stroke's) doesn't flick in and out.

### Splashes and ripples
The Character prefab's WaterEffects plays the character's splashes and ripples on water. Movement only reports what happens (its events and state); this plays the effects:
- **Splashes**, on the surface where the character goes in or comes out: falling or diving in, jumping, climbing or vaulting out, surfacing, landing in shallow water (by how fast it was going up or down) and running into it (by how fast it was going along). A splash is at full strength at Splash Full Speed, and slower than Splash Min Strength of that there's none; with Splash Ripples on, it sets off a ripple too. One physics step makes at most one splash, however many of these happen in it.
- **Ripples**, on the surface round the character as it swims or wades: one every Ripple Spacing it goes, and every Idle Ripple Interval while it's still (slower than Ripple Moving Speed). Not underwater.

The effects are prefabs, its Splash Prefab and Ripple Prefab, and can be anything. A prefab with a component implementing `IWaterEffect` plays that (`Play` is given where on the surface, how strong, the character's size and its velocity); otherwise its particle systems play as they are, scaled with the character's size and, down to Weakest Scale, the effect's strength (give them Hierarchy scaling). They're pooled: no more than Max Playing of each play at once, the oldest reused after that. Splashed and Rippled are raised for each, with or without a prefab, for sound and the like. Empty slots play nothing.

The defaults are a burst of droplets and a pair of spreading rings, each one particle system drawn with the Water Effect shader (Assets/MeridianMotion/Content/Materials/WaterEffect.shader: soft droplets or rings built from each particle's shape and noise, no textures), in Assets/MeridianMotion/Content/Prefabs/Effects. Tools > Meridian Motion > Utilities > Create Water Effects makes them (and their materials) where they're missing and gives them to the Character prefab where its slots are empty; after that they're kept, so edit them freely, or delete one to have it made afresh.

### The view underwater
An UnderwaterView on a camera gives it a view from under the water: while the camera is in a body of water, the scene's fog becomes the water's (its Fog Color, at its Fog Density), so what's under the water fades into it with distance, and the scene's sun is tinted by its Light Tint and dimmed to its Light Intensity share, as light coming down through water is, so even what's near takes on the water's colour. Back out of the water, the scene's own fog and sun are as they were. A camera within its Surface Margin above the surface counts as under it, as its near plane is already there. It knows nothing of the character: only its camera's position and the water. Set Up Scene puts one on the camera of a scene with water. It works through the scene's fog and sun, so it needs nothing of the render pipeline; for more (a post-processing volume's colour grading, muffled sound), its CameraSubmerged and CameraSurfaced events say when the camera goes under and comes up. The camera isn't kept off the surface yet, so a camera right at it shows the line across its view.

### Wading
Walking or running through water too shallow to swim in slows the character, the more the deeper it is: at full speed where the water over its feet reaches Wade Start Depth (a share of its standing height, so puddles don't slow it), easing down to Wade Speed Multiplier of its speed where it's as deep as Swimming's Swim Depth, where it would start swimming. It changes only the speed the character walks, runs or crouch-walks at, as crouching does: air steering, dashes and jumps aren't slowed, and something low is still vaulted by walking into it, as on dry land (Vaulting's Vault From Ground turns that off). Wading raises WadeStarted and WadeEnded, for splashes and sounds. The StandardMovementMode asset's Wading component tunes it; turning it off, a Wade Speed Multiplier of 1, or a Wade Max Depth of 0, lets the character walk through water at full speed. Wade Max Depth (a share of its standing height too) is the deepest water it wades in: below 1, deeper water too shallow to swim in is walked through as on land. There's no wading animation yet: the walk just plays slower.
