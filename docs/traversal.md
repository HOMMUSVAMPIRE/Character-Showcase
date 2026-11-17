# Climbing and Traversal
Climbing walls and ladders, pushing and pulling objects, ropes, ziplines, bounce pads, swimming and wading. Each is a component of the BasicMovementMode asset (Assets/ScriptableObjects/BasicMovementMode.asset; see [Movement Components](movement.md#movement-components)), in Assets/Scripts/MovementModes/Components, with its settings under its name in the asset's Components list. The settings named below are on that asset unless another component is named.

## Wall Climbing
Surfaces with a ClimbableSurface component (on a collider, or on a parent to make a whole group climbable) can be climbed, and so can the parts of an object painted climbable with a PaintedClimbableSurface (see [Painting climbable areas](#painting-climbable-areas)); nothing else can. A climbable wall is climbed instead of wall-run along or slid down.

**Taking hold.** Touching a climbable surface while heading anywhere remotely into it, on the ground or in mid-air, takes hold of it: steering into it, or moving into it at least Wall Climb Min Approach Speed, within Wall Climb Start Max Angle of head-on (anything but sliding along it), and within Wall Climb Start Reach of it. Holding onto a climbable surface counts as touching the ground: taking hold gives back the character's jumps and air dashes.

**Moving on it.** Climbing, the character faces the surface without gravity and holds still without input. The stick moves it over the surface at Wall Climb Speed (times the surface's own Speed Multiplier): up and down with the stick's up and down, sideways with left and right, as the camera sees it, so up on the stick climbs up whichever side of the character the camera is on. Wall Climb Snap Speed draws it in against the surface, so it stays against it as the surface curves.

**The climbable area holds the character in.** What has to stay on it is the character's grip, a point on its body Wall Climb Grip Height (a share of its height) above its feet. The character climbs on as long as the grip stays within a few centimetres of a climbable surface (`WallClimbEdgeTolerance` in Components/WallClimbing.cs), and stops where it wouldn't: a move that would take it off is cut down to whatever part of it (up, down or sideways) stays on.

It goes round outside corners onto the next climbable face, its grip swinging round the edge, and into inside corners onto the climbable wall it meets, so climbable surfaces that join are climbed as one. Where something unclimbable comes next instead (the plain wall round a climbable panel set on it, or a ledge that isn't climbable), it stops at the edge.

**Climbing over the top.** Pushing up with a walkable top within reach of its hands (the window a ledge is grabbed in, Ledge Grab Min Height to Ledge Grab Max Height: see [Ledges](abilities.md#ledges)), it climbs up onto the top as a ledge climb does from hanging, and hangs from the edge if there's no room up there. A top it climbs past before reaching one (leaning back further than Wall Climb Max Lean Back from upright, or past the top of the climbable area) is climbed onto from there. Surfaces leaning out over the character by up to Wall Climb Max Overhang can be climbed too.

**Stepping on and off.** Taking hold from standing on the ground, the character first steps up onto the surface, holding still meanwhile, for Wall Climb Enter Duration. Pressing down at the bottom with the feet on the ground steps back off it onto the ground the same way, for Wall Climb Exit Duration (0 for either does it at once). Taking hold in mid-air, letting go and jumping off don't step.

**Letting go.** Jump kicks off the surface, upward (Wall Climb Jump Up Speed) and away (Wall Climb Jump Away Speed), giving back air jumps as a wall jump does ([Wall-Running](abilities.md#wall-running)). Crouch lets go and drops. After letting go, nothing can be climbed for Wall Climb Regrab Delay.

The character climbs on surfaces that move, carried with them. The BasicMovementMode asset's Wall Climbing component tunes it all; turning it off, or a Wall Climb Speed of 0, turns climbing off. Climbing over the top of a surface is a ledge climb, so it needs Ledges turned on.

### Painting climbable areas
To make only part of an object climbable (one face of a box, a strip up a wall, a patch of handholds), add a PaintedClimbableSurface to it and paint the climbable regions in the Scene view:

- With the object selected, press Paint in the component's inspector, or pick Paint Climbable Surface in the Scene view's Tools overlay. It's an Editor Tool, so while it's on, dragging paints instead of selecting.
- Drag over the object's colliders (its own and its children's) to paint, and Shift-drag to erase; [ and ] size the brush.
- The paint shows as a green overlay whenever the object is selected, and strokes can be undone. Paint and Erase can also be called from code.

Paint is kept as cells in the object's own space, so it moves, turns and stretches with the object. The cells' size is the component's Cell Size setting, and changing it clears the paint. Each cell is painted for the face it was painted on, so painting the front of a wall doesn't paint its sides or back. The character climbs only where its grip is over paint, stopping at the paint's edges as at any other edge of the climbable area, and goes round a corner only where the face round it is painted too.

**Seeing the paint in the game.** Painted areas show on materials made to show them. The levels' concrete shader (NoiseConcrete) does, with a Climbable Mask texture and a second set of its look's settings for climbable areas (Climbable Tint, Climbable Grain, Climbable Panels and the rest of them; by default an ochre, rougher concrete in finer panels), blended in where the mask is white.

When a stroke's done, it paints a mask texture for each of the object's renderers with such a material, in the mesh's UV2, at the component's Mask Size resolution, and gives it to the renderer as per-renderer data, so the material stays shared. The masks are saved as assets in Assets/ClimbablePaint, a folder per scene. They're optional: turn Paint Masks off and the material shows none. Every unpainted object shows the material's own mask, which for all the level's materials is ClimbableMask_None, a single black pixel (Tools > Character Showcase > Set Up Climbable Materials gives it to any material with a climbable mask that has none).

**The mesh needs a UV2** that gives each face its own space. The first UV set won't do: Unity's cube puts all six faces on the same square.

- The level builders give their meshes one (each face a chart of its own, packed into the square at one scale; the grass tufts, which nothing's painted on, are left out), and the level's boxes use Graybox Cube (Assets/Models/Level), Unity's cube with a UV2.
- On the first stroke, painting onto Unity's cube elsewhere swaps it for Cube (UV2) (in Assets/ClimbablePaint), and a mesh in the scene for a copy unwrapped by Unity.
- An imported model needs Generate Lightmap UVs on in its import settings, which the component's inspector offers to turn on (it changes the model for everything using it).

### Climbing animations
Climbing plays the Quaternius library's climbing loops (Universal Animation Library Pro: see [Quaternius animations](animation.md#quaternius-animations)), in a Wall Climb state entered while the WallClimbing parameter is set. The state blends the library's climbing idle and its climbs up, down, left and right by which way and how fast the character climbs: the ClimbRight and ClimbUp parameters, in m/s across and up the surface, at the authored size. Each climbing loop plays at the speed its hands are made for, and faster past it, however fast the climb is tuned (see [Walking and running](animation.md#walking-and-running)).

Each loop is placed in the blend at the speed its holding hands are made for, measured on the mannequin when it's generated (`MotionCaptureLocomotion.ClimbSpeed`, in Assets/Editor/Animation/MotionCaptureLocomotion.cs), and played faster above it, so the hands keep their holds at any climbing speed. WallSlideIK moves the body in toward the surface until the nearer hand is on it (the clips are made for a wall nearer the body than the collider lets it come), easing between holds; foot placement is off while climbing.

Stepping onto the surface from the ground and off it onto the ground play the library's climbing enter and exit (Climb Enter and Climb Exit states, timed by ClimbStepProgress, while ClimbEntering or ClimbExiting is set). Climbing up onto a ledge, from hanging or from climbing a wall, plays its ClimbLedge, which starts from a climbing pose (UAL2's ClimbUp_1m without the Pro library). Without the library, the mannequin's hang pose shows, with no enter or exit.

The AnimationController passes climbing on to animation: CharacterAnimationState.wallClimbing, with the surface in wallPoint and wallNormal, and the climb's speeds in localVelocity (x to the right, y up).

## Ladders
A Ladder component makes a ladder: set its Height (and Width, Rung Spacing, rail and rung thickness) in the inspector, and with Build Mesh on it makes its own rails, rungs and box collider to match whenever they change (Add Component > Ladder on an empty object; give it a Material). Its origin is the middle of its foot, and it's climbed from its front, the side its +Z faces. In the levels, the Graybox Ladder piece builds one (orange rails, steel rungs) with its back against a wall, which Set Up Scene ([spawning.md](spawning.md)) makes climbable; the Level Playground ([levels.md](levels.md)) has one up the side of the lift's landing.

**Climbing it.** A ladder is climbed as a climbable surface is (see [Wall Climbing](#wall-climbing)), with the same settings, but only straight up and down it, on its middle, and only from its front:

- Heading into its front takes hold of it (from the ground, stepping onto it first). Up and down on the stick climb it, sideways does nothing, and the character is held on the ladder's middle, facing it.
- It climbs as far as the ladder's top and no further, and climbs off the top onto a floor there when its hands reach it (a ladder standing on its own isn't climbed over).
- Pressing down at the bottom steps off; Jump kicks off it and Crouch lets go.
- A ladder gives back jumps and air dashes as other climbing does, and moves the character with it if it moves.

**Animating it.** Climbing a ladder is animated procedurally. The Animator's Ladder state holds a climbing pose (the library's climbing idle), and LadderIK, on every model, puts the hands on the rungs (just in from the rails) and the feet on the rungs (or the ground), and draws the body in to the ladder.

Where each hand and foot is depends only on how high the character is up the ladder, so it's the same climbing up or down at any speed, and holding still holds every rung. Each limb holds a rung while the body climbs past it, then, over the last part of each two rungs' climb (LadderIK's Swing Share), moves up two rungs, out from the ladder and back, in diagonal pairs: the left hand with the right foot, then the right hand with the left. A hand holds rungs up to LadderIK's Hand Reach above the feet, and a foot stands on them up to its Foot Reach (at the authored size). It eases in over stepping on and out over stepping off. LadderIK's other settings tune the placement and swing.

(The CMU motion-capture set has two ladder climbs, in Assets/Animation/CMU/Climb Hang, but they fit one ladder's rungs, which the procedural climb fits to any.)

## Pushing and Pulling
Large objects with a PushableObject component (a kinematic Rigidbody with a BoxCollider) can be pushed and pulled.

**Taking hold.** Holding Interact (E, or the north face button) facing one of an object's sides, close to it (within Grab Reach, and within Grab Max Angle of head-on), takes hold of it. Steering toward it then pushes it, and away from it pulls it, at Push Pull Speed times the object's own Speed Multiplier (lower for heavier objects); sideways steering does nothing. Objects can be made push-only (Can Be Pulled, on the PushableObject).

**Moving it.** An object's moves are swept against the world, so it stops at walls, and the character stops with it. Pushed off an edge, an object falls (it has simple gravity of its own: the PushableObject's Gravity and Max Fall Speed) and the character lets go. Letting go of Interact or leaving the ground lets go too. Holding an object, the character faces it and can't jump, dash, crouch or roll. Without Interact held, an object is just an obstacle.

The BasicMovementMode asset's Pushing And Pulling component tunes the speed and reach.

## Ropes
A Rope component makes a rope to swing on: it hangs from its object's position (its anchor), Length metres long, and with Build Mesh on it draws itself (Add Component > Rope on an empty object where the rope is tied; give it a Material). Nothing collides with a rope: characters find it by where it is. In the levels, the Graybox Rope piece hangs one from a bracket, which Set Up Scene makes swing; the Level Playground has one on a gantry over a gap too wide to jump.

**Catching hold.** Reaching a rope in mid-air catches hold of it: it has to pass within Rope Grab Reach of the character's hands, which hold it Rope Grip Height (a share of the character's height) above its feet. The hands catch it where they are, but never nearer the anchor than Rope Min Grip Distance. Catching hold gives back jumps and air dashes.

**Swinging.** The character swings on the rope as a pendulum about the anchor, carried on by the speed it caught hold with: run and jump at a rope and the swing takes you on past it and up.

- Gravity swings it (the character's own, so a rope swings as quickly as the character falls).
- The stick pumps it at Rope Swing Acceleration, pushing the way it points: push with the swing to swing higher.
- It loses Rope Swing Damping (a share of its speed) every second, so without input it slowly dies away and never rises higher.
- It swings out no further than Rope Max Swing Angle from hanging straight down, and no faster than Rope Max Swing Speed.
- Something in the way stops the swing, the character staying on the rope. The character faces the way it steers.

**Letting go.** Jump lets go with the swing's speed and a jump up (Rope Jump Up Speed); Crouch, or touching the ground, lets go. Let go of, the rope swings on by itself, slowing by the Rope's own Swing Damping until it hangs straight again, and can be caught on its way back. The same rope can't be caught again for Rope Regrab Delay after letting go (another can, so the character can swing from rope to rope).

The rope is drawn between physics steps as the character is, so the hands stay on it. The BasicMovementMode asset's Rope Swinging component tunes it; turning it off, or a Rope Grab Reach of 0, turns rope swinging off.

## Ziplines
A Zipline component makes a zipline: a straight cable from its object's position to its End Point (in its own space), ridden at up to its Max Speed, which with Build Mesh on draws its own cable. In the levels, the Graybox Zipline piece builds one between two posts, which Set Up Scene makes rideable; the Level Playground has one from the top of a tower down to the ground.

**Catching hold.** Reaching the cable in mid-air (it passing within Zipline Grab Reach of the character's hands: walking off a platform under it, say, or jumping up to it) catches hold of it. The character rides along it, hanging below it (its feet Zipline Hang Height, a share of its height, under the cable) and facing the way it rides. Catching hold gives back jumps and air dashes.

**The ride.** The ride starts at the character's speed along the cable, either way. Gravity along the cable speeds it up downhill and slows it uphill, and drag takes Zipline Drag (a share of its speed) every second. Downhill it goes at least Zipline Min Speed, so a gentle cable still gets the rider to its end (caught heading uphill, it slows, stops and comes back), and never faster than the zipline's Max Speed. At the end it's heading for, it lets go and carries on at the ride's speed.

**Letting go.** Jump lets go with a jump up (Zipline Jump Up Speed) on top of the ride's speed; Crouch, touching the ground, or being stopped by a wall or a ceiling lets go. The same zipline can't be caught again for Zipline Regrab Delay after letting go.

The BasicMovementMode asset's Ziplines component tunes it; turning it off, or a Zipline Grab Reach of 0, turns ziplines off.

## Bounce Pads
A BouncePad component on an object makes everything collidable on it a bounce pad. Standing on one (landing on it, or walking onto it) launches the character along the pad's up, fast enough that its feet rise the pad's Launch Height above it (worked out for the physics steps, so the height is exact), keeping the pad's Keep Momentum share of its speed across it (by default all of it, so running across a pad carries the run on). Holding Jump as it comes down on the pad multiplies the height by Bounce Jump Boost. A tilted pad launches along its tilt.

The launch counts as the character's jump: its air jumps and dashes are left, and it ends whatever else the character was doing (a dash, roll or slide, or holding a rope or a zipline). A pad can squash its Spring (its springy top) and let it spring back on each launch, for show, and raises Launched for effects and sound.

In the levels, the Graybox BouncePad piece builds one (a steel base with a cyan top), which Set Up Scene makes bounce; the Level Playground has one up onto its zipline tower. The BasicMovementMode asset's Bounce Pads component holds Bounce Jump Boost and Bounce Height Multiplier, which every pad's height is multiplied by; turning it off, or a multiplier of 0, turns pads off.

## Swimming
A Water component makes a body of water: a box whose top, at its object's position, is the water's surface, reaching down and across its Size (width, depth and length, in its own space; it may be turned about the vertical, not tilted). Nothing collides with water: the character finds it as it finds ropes and ziplines, and stands on whatever is built under it (a pool's floor). With Build Mesh on, it draws its own flat surface in its Material. In the levels, the Graybox Water piece draws the surface over a footprint, which Set Up Scene makes swimmable; the Level Playground has a pool.

**Getting in.** Where the water over the character's feet is at least Swim Depth deep (a share of its standing height), it swims: walking down into it, or falling or jumping in. Getting in gives back jumps and air dashes, as touching the ground does, and ends whatever else the character was doing (a dash, roll or slide). Shallower water is walked through.

**Floating.** The character swims at the surface, its feet hanging Swim Depth below it, floating up off a beach walked down into, or off the bottom. Falling in, it plunges deeper and floats back up: the water's drag takes Swim Plunge Drag (a share of its speed) for each metre it plunges, so a plunge from high goes only a little deeper than one from low, and it rises back no faster than Swim Surfacing Speed, settling at its depth without bobbing past it.

**Swimming.** The character swims the way the movement input points, relative to the camera, at Swim Speed (Swim Sprint Speed with Sprint held), facing the way it swims. Its speed changes at Swim Acceleration: speeding up, turning, slowing to a stop without input, and taking away the speed it falls in with. It can't dash, crouch or dive: swimming is only at the surface.

**Getting out.** Jump jumps out, its feet rising Swim Jump Height above the surface: enough to climb out onto an edge a little above the water, or to catch a ledge. It's the character's jump from the ground, so its air jumps are left. Swimming into shallows, it finds its feet and walks out once it stands on a floor a little shallower than Swim Depth (so wading at the edge doesn't flicker in and out of swimming). Swimming into something whose top sticks out of the water no more than Swim Vault Height (a share of the standing height) vaults over or onto it, as a vault on land does: onto a pool's side, or over a low divider back into the water beyond (handed to Vaulting; a Swim Vault Height of 0 never vaults from the water). It's shown as climbing out (the ledge climb's animation, its hands on the edge), not as a vault. Swimming into a taller edge whose top is within reach of the character's hands (no higher above its feet than Ledges' Ledge Grab Max Height) climbs out onto it, as climbing over a ledge's top. A ladder or a climbable wall rising out of the water is climbed out too (Wall Climbing comes before Swimming in the components), straight over its top if that's within reach. A side higher than that is only jumped onto. Off the water's edge, it carries on as it was going.

The BasicMovementMode asset's Swimming component tunes it; turning it off, or a Swim Depth of 0, turns swimming off (the character sinks to the bottom and walks there). The swimming animation is floated at the default character's Swim Depth: after retuning it, run Generate Mannequin again (see [Swimming animations](animation.md#swimming)).

### Diving
At the surface, Crouch dives: the character is pushed down under the surface at Swim Dive Speed and swims underwater. Underwater it swims the way the movement input points relative to the camera, up and down with it (looking down and pushing forward swims down), at Swim Speed (Swim Sprint Speed with Sprint held). Holding Jump rises and holding Crouch sinks, at Swim Vertical Speed, whichever way the camera looks, so it can look at something while it goes up or down. With no input at all it drifts up at Swim Buoyancy; back up at its swimming depth, rising, it's swimming at the surface again (a press of Jump underwater only rises: it doesn't jump out on arriving). Speeds change at Swim Acceleration, and it faces the way it swims across.

Underwater its body lies flat along the way it swims, resting on its feet, so it swims along the floor and under what a swimmer would (the Level Playground's arch). It stands again at the surface. Lying, it turns only as far as it has room to (in a narrow channel, say: see [Collision](movement.md#collision)). Steered up or down, its body pitches along the way it swims, nose first, up to Swim Max Pitch (0 keeps it level), as far as it has room to; it levels out without steering, and doesn't pitch down into the pool's floor it's swimming along. The swimming animations float the body Swim Depth above the feet, as treading water at the surface needs, so underwater the model is drawn lowered to the body, easing down as it dives and back up as it surfaces over the Animation Controller's Model Drop Smooth Time. Diving raises DiveStarted, and DiveEnded when it's back at the surface or leaves the water. A Swim Dive Speed of 0 turns diving off. Underwater, the Animator plays a state of its own (Swim Underwater: see [Swimming animations](animation.md#swimming)).

There's no breath in this package: for a game's own (or for muffling sound, or an underwater look), the AnimationController raises HeadSubmerged when the character's head goes under a water's surface and HeadSurfaced when it comes back above, and says whether it's under now (IsHeadUnderwater, HeadWater). The head is the model's head bone as it's posed each frame, or its eye point for a model without a humanoid head; it must go Head Water Margin (on the AnimationController) past the surface either way to count, so a head bobbing at the water line (the swim stroke's) doesn't flick in and out.

### Splashes and ripples
The Character prefab's WaterEffects plays the character's splashes and ripples on water. Movement only reports what happens (its events and state); this plays the effects:
- **Splashes**, on the surface where the character goes in or comes out: falling or diving in, jumping, climbing or vaulting out, surfacing, landing in shallow water (by how fast it was going up or down) and running into it (by how fast it was going along). A splash is at full strength at Splash Full Speed, and slower than Splash Min Strength of that there's none; with Splash Ripples on, it sets off a ripple too. One physics step makes at most one splash, however many of these happen in it.
- **Ripples**, on the surface round the character as it swims or wades: one every Ripple Spacing it goes, and every Idle Ripple Interval while it's still (slower than Ripple Moving Speed). Not underwater.

The effects are prefabs, its Splash Prefab and Ripple Prefab, and can be anything. A prefab with a component implementing `IWaterEffect` plays that (`Play` is given where on the surface, how strong, the character's size and its velocity); otherwise its particle systems play as they are, scaled with the character's size and, down to Weakest Scale, the effect's strength (give them Hierarchy scaling). They're pooled: no more than Max Playing of each play at once, the oldest reused after that. Splashed and Rippled are raised for each, with or without a prefab, for sound and the like. Empty slots play nothing.

The defaults are a burst of droplets and a pair of spreading rings, each one particle system drawn with the Water Effect shader (Assets/Materials/WaterEffect.shader: soft droplets or rings built from each particle's shape and noise, no textures), in Assets/Prefabs/Effects. Tools > Character Showcase > Create Water Effects makes them (and their materials) where they're missing and gives them to the Character prefab where its slots are empty; after that they're kept, so edit them freely, or delete one to have it made afresh.

### The view underwater
An UnderwaterView on a camera gives it a view from under the water: while the camera is in a body of water, the scene's fog becomes the water's (its Fog Colour, at its Fog Density), so what's under the water fades into it with distance, and the scene's sun is tinted by its Light Tint and dimmed to its Light Intensity share, as light coming down through water is, so even what's near takes on the water's colour. Back out of the water, the scene's own fog and sun are as they were. A camera within its Surface Margin above the surface counts as under it, as its near plane is already there. It knows nothing of the character: only its camera's position and the water. Set Up Scene puts one on the camera of a scene with water. It works through the scene's fog and sun, so it needs nothing of the render pipeline; for more (a post-processing volume's colour grading, muffled sound), its CameraSubmerged and CameraSurfaced events say when the camera goes under and comes up. The camera isn't kept off the surface yet, so a camera right at it shows the line across its view.

### Wading
Walking or running through water too shallow to swim in slows the character, the more the deeper it is: at full speed where the water over its feet reaches Wade Start Depth (a share of its standing height, so puddles don't slow it), easing down to Wade Speed Multiplier of its speed where it's as deep as Swimming's Swim Depth, where it would start swimming. It changes only the speed the character walks, runs or crouch-walks at, as crouching does: air steering, dashes and jumps aren't slowed, and something low is vaulted by walking into it, not only sprinting as on dry land (Vaulting's Vault From Ground turns both off). Wading raises WadeStarted and WadeEnded, for splashes and sounds. The BasicMovementMode asset's Wading component tunes it; turning it off, or a Wade Speed Multiplier of 1, lets the character walk through water at full speed. There's no wading animation yet: the walk just plays slower.

### The water's look
The levels' water is drawn with its own shader, Assets/Materials/Water.shader (the Level_Water material, made the first time a level's built), whose whole look is built from noise in world space with no textures, as the other surfaces' is:
- layers of drifting noise make the ripples, which bend the surface's normal (Ripple Size, Ripple Speed, Ripple Strength) and lift and lower it a little (Wave Height);
- the sky reflects in it, more at a glancing look, and the sun glints off the ripples (Smoothness, Sky Reflection, Sun Glints);
- how much water is under each point (from the camera's depth texture) tints it from Shallow Colour to Deep Colour, from see-through to nearly opaque (Depth To Deep, Shallow Opacity, Deep Opacity);
- and foam rims what stands in the water, broken up by noise (Foam Colour, Foam Distance, Foam Breakup).

From under the surface it's a murky ceiling. The depth effects need the render pipeline asset's Depth Texture on (the PC asset has it); without it, the water all looks deep and has no foam. A camera that goes under the water sees the pool clearly: there's no underwater fog yet.
