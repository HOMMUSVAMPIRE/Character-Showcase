# CMU motion capture clips

A selection of the CMU Graphics Lab Motion Capture Database (http://mocap.cs.cmu.edu), as converted to FBX in the
"Anims_Only_FBX_V1" pack. See LICENSE.txt and README.txt (from the pack, kept unchanged): free to use, modify and
redistribute, but not to sell as animations, and anything passed on must carry the same terms.

"The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217."

## Changes from the pack

- **Root motion removed.** Each clip's hips no longer travel across the ground: at each frame, a straight line fitted to
  the hips' path over the second around it is taken out, so steady travel goes but the sway within each step stays,
  and the hips keep their height (jumps still rise and fall). Every other bone is as captured. Done in Blender by
  Tools/Animation/remove_root_motion.py, which also checks each exported file against the edit.
- **Selected and renamed.** One take of each motion kept (of 2,548 in the pack), each named for what it shows from
  CMU's own description, followed by its CMU subject and trial number (`subject_trial`), and sorted into folders by
  kind of motion.

- **T-pose rest pose.** Each clip's first frame is a T-pose; it's made the skeleton's rest pose, with the motion re-keyed
  against it (Tools/Animation/tpose_rest.py), so it's each avatar's reference pose in Unity.
- **Humanoid in Unity.** Tools > Character Showcase > Prepare Motion Capture Clips makes each a Humanoid animation with its
  own avatar, mapped by the CMU skeleton's bone names, starting after the T-pose frame, its height and remaining sway
  baked into the pose, and facing the way the body does.

Below, each clip's full CMU description, with the subject's overall description.

## Carry Push Pull (9)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Carry 5.5lb suitcase (70_01).fbx | 70_01 | carry 5.5lb suitcase | suitcase |
| Push heavy object (81_05).fbx | 81_05 | push heavy object | pushing a box; jumping off a ledge; walks |
| Push heavy box (81_06).fbx | 81_06 | push heavy box | pushing a box; jumping off a ledge; walks |
| Pull heavy object (81_07).fbx | 81_07 | pull heavy object | pushing a box; jumping off a ledge; walks |
| Drag heavy object (81_08).fbx | 81_08 | drag heavy object | pushing a box; jumping off a ledge; walks |
| Pushing on heavy object (82_06).fbx | 82_06 | Pushing on heavy object | jumping; pushing; emotional walks |
| Walk and carry (113_26).fbx | 113_26 | Walk and carry | Post pregnant woman |
| Push turn left (134_06).fbx | 134_06 | Push Turn Left | Skateboard Motions |
| Push turn right (134_08).fbx | 134_08 | Push Turn Right | Skateboard Motions |

## Climb Hang (9)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Playground - climb (01_02).fbx | 01_02 | playground - climb | climb, swing, hang on playground equipment |
| Playground - climb, hang, swing (01_03).fbx | 01_03 | playground - climb, hang, swing | climb, swing, hang on playground equipment |
| Playground - climb, hang, hold self up with arms straight (01_09).fbx | 01_09 | playground - climb, hang, hold self up with arms straight, swing, drop, sit, dangle legs, go under | climb, swing, hang on playground equipment |
| Playground - climb, hang, lean over, jump down (01_11).fbx | 01_11 | playground - climb, hang, lean over, jump down | climb, swing, hang on playground equipment |
| Playground - climb, pull up, dangle, sit, lower self to (01_12).fbx | 01_12 | playground - climb, pull up, dangle, sit, lower self to ground | climb, swing, hang on playground equipment |
| Climb ladder (13_33).fbx | 13_33 | climb ladder | various everyday behaviors |
| Playground - grip bar, swing body (43_02).fbx | 43_02 | playground - grip bar, swing body | swing on playground equipment |
| Climb backwards off ledge (81_14).fbx | 81_14 | climb backwards off ledge | pushing a box; jumping off a ledge; walks |
| Climb up and down ladder (143_37).fbx | 143_37 | Climb Up And Down Ladder | General Subject Capture |

## Crouch Sneak Crawl (14)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Walk stealthily (17_03).fbx | 17_03 | walk stealthily | different walking styles |
| Duck to avoid flying object (77_09).fbx | 77_09 | duck to avoid flying object | careful actions |
| Careful creeping (77_14).fbx | 77_14 | careful creeping | careful actions |
| Crawling (111_03).fbx | 111_03 | Crawling | Pregnant Woman |
| Run duck underneath (127_29).fbx | 127_29 | Run Duck Underneath | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Walk crawl (133_01).fbx | 133_01 | Walk Crawl | Baby Styled Walk |
| Duck under (134_01).fbx | 134_01 | Duck Under | Skateboard Motions |
| Walk crouched (136_09).fbx | 136_09 | Walk Crouched | Weird Walks |
| Walk backwards crouched (136_11).fbx | 136_11 | Walk Backwards Crouched | Weird Walks |
| Walk on toes crouched (136_13).fbx | 136_13 | Walk on Toes Crouched | Weird Walks |
| Run to sneak (139_11).fbx | 139_11 | Run To Sneak | Action Walks, sneaking, wounded, looking around |
| Sneak sideways (139_14).fbx | 139_14 | Sneak Sideways | Action Walks, sneaking, wounded, looking around |
| Sneaking (139_29).fbx | 139_29 | Sneaking | Action Walks, sneaking, wounded, looking around |
| Sneak (143_41).fbx | 143_41 | Sneak | General Subject Capture |

## Falls Stumbles Balance (8)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Balance on one leg, outstretched arms (49_18).fbx | 49_18 | balance on one leg, outstretched arms | modern dance, gymnastics |
| Fall on face (90_16).fbx | 90_16 | fall on face | cartwheels; acrobatics; dances |
| Rug pull fall (90_18).fbx | 90_18 | RugPullFall | cartwheels; acrobatics; dances |
| 360 small stumble (91_59).fbx | 91_59 | 360 smallStumble | walks and turns |
| Stumble walk (104_13).fbx | 104_13 | StumbleWalk | motion |
| Roll over (111_21).fbx | 111_21 | Roll over | Pregnant Woman |
| Run roll underneath (128_09).fbx | 128_09 | Run Roll Underneath | Action Adventure Motions running, ducking, rolling, stopping |
| Walk with arms out, balancing (132_01).fbx | 132_01 | Walk With Arms Out, balancing | Varying Weird Walks |

## Idle (3)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Static pose (82_01).fbx | 82_01 | Static pose | jumping; pushing; emotional walks |
| Standing still (111_28).fbx | 111_28 | Standing still | Pregnant Woman |
| Idle (140_06).fbx | 140_06 | Idle | Getting Up From Ground |

## Jump Hop (25)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Jump up to grab, reach for, tiptoe (13_10).fbx | 13_10 | jump up to grab, reach for, tiptoe | various everyday behaviors |
| Jump (16_01).fbx | 16_01 | jump | run, jump, walk |
| High jump (16_03).fbx | 16_03 | high jump | run, jump, walk |
| Forward jump (16_05).fbx | 16_05 | forward jump | run, jump, walk |
| Run, leap (49_04).fbx | 49_04 | run, leap | modern dance, gymnastics |
| Run jump (75_01).fbx | 75_01 | run jump | jumps; hopscotch; sits |
| 180 jump (75_06).fbx | 75_06 | 180 jump | jumps; hopscotch; sits |
| Box jump (75_12).fbx | 75_12 | box jump | jumps; hopscotch; sits |
| Jump backwards off ledge (81_12).fbx | 81_12 | jump backwards off ledge | pushing a box; jumping off a ledge; walks |
| Jump off ledge (82_02).fbx | 82_02 | Jump off ledge | jumping; pushing; emotional walks |
| Large step to a short ledge (83_03).fbx | 83_03 | large step to a short ledge | steps |
| Hop forward (83_40).fbx | 83_40 | hop forward | steps |
| Long jump forward (83_42).fbx | 83_42 | long jump forward | steps |
| Jump small forward (91_43).fbx | 91_43 | JumpSmallForward | walks and turns |
| Jump turn (91_46).fbx | 91_46 | Jump Turn | walks and turns |
| Jump (118_01).fbx | 118_01 | Jump | Jumping |
| Run jump stop run (127_21).fbx | 127_21 | Run Jump Stop Run | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run dive over roll run (127_23).fbx | 127_23 | Run Dive Over Roll Run | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run jump over (127_25).fbx | 127_25 | Run Jump Over | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Jump stop (131_07).fbx | 131_07 | Jump Stop | Michael Jackson Styled Motions |
| Hop on left foot (132_23).fbx | 132_23 | Hop on left foot | Varying Weird Walks |
| Walk jump (133_03).fbx | 133_03 | Walk Jump | Baby Styled Walk |
| Jump sideways (141_05).fbx | 141_05 | Jump Sideways | General Subject Capture |
| Walk up and over (141_07).fbx | 141_07 | Walk Up and Over | General Subject Capture |
| Step over (141_09).fbx | 141_09 | Step Over | General Subject Capture |

## Pick Up (8)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Bend, pick up (26_09).fbx | 26_09 | bend, pick up | nursery rhymes, basketball, bending |
| Walk up to object, squat, pick up object, set down in (69_70).fbx | 69_70 | walk up to object, squat, pick up object, set down in another place. | walking |
| Lifting up (74_07).fbx | 74_07 | lifting up | kicks and walking on slopes |
| Picking up something (80_08).fbx | 80_08 | picking up something | assorted motions |
| Pick box up, bend from waist (115_02).fbx | 115_02 | Pick box up, bend from waist | Bending over |
| Picking box up, bending knees (115_06).fbx | 115_06 | Picking box up, bending knees | Bending over |
| Normal pick up (137_27).fbx | 137_27 | Normal Pick Up | Stylized Motions |
| Walk and pick up box (143_11).fbx | 143_11 | Walk And Pick up Box | General Subject Capture |

## Run (17)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Run (09_01).fbx | 09_01 | run | run |
| Run, veer left (16_48).fbx | 16_48 | run, veer left | run, jump, walk |
| Run, veer right (16_49).fbx | 16_49 | run, veer right | run, jump, walk |
| Run, 90-degree left turn (16_51).fbx | 16_51 | run, 90-degree left turn | run, jump, walk |
| Run, 90-degree right turn (16_53).fbx | 16_53 | run, 90-degree right turn | run, jump, walk |
| Run or jog (35_17).fbx | 35_17 | run/jog | walk, run |
| Running straight (78_06).fbx | 78_06 | RunningStraight CleanedGRS | walking |
| Start jog (104_06).fbx | 104_06 | StartJog | motion |
| Jog stop (104_09).fbx | 104_09 | JogStop | motion |
| Start run (104_53).fbx | 104_53 | StartRun | motion |
| Walk to run (127_04).fbx | 127_04 | Walk to Run | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run to quick stop (127_05).fbx | 127_05 | Run to Quick Stop | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run right (127_09).fbx | 127_09 | Run Right | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run left (127_11).fbx | 127_11 | Run Left | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run side step left (127_13).fbx | 127_13 | Run Side Step Left | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run side step right (127_14).fbx | 127_14 | Run Side Step Right | Action Adventure Obstacles, running, jumping, ducking, rolling |
| Run stop run (127_17).fbx | 127_17 | Run Stop Run | Action Adventure Obstacles, running, jumping, ducking, rolling |

## Sit Stand (12)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Sit on high stool, stand up (13_01).fbx | 13_01 | sit on high stool, stand up | various everyday behaviors |
| Sit on stepstool, chin in hand (13_04).fbx | 13_04 | sit on stepstool, chin in hand | various everyday behaviors |
| Medium sit (75_17).fbx | 75_17 | medium sit | jumps; hopscotch; sits |
| High sit (75_18).fbx | 75_18 | high sit | jumps; hopscotch; sits |
| Low sit (75_20).fbx | 75_20 | low sit | jumps; hopscotch; sits |
| Lay down and get up (113_08).fbx | 113_08 | Lay down and get up | Post pregnant woman |
| Sit in chair and get up (113_15).fbx | 113_15 | Sit in chair and get up | Post pregnant woman |
| Get up face down (140_01).fbx | 140_01 | Get Up Face Down | Getting Up From Ground |
| Get up laying on side (140_03).fbx | 140_03 | Get Up Laying on Side | Getting Up From Ground |
| Get up from ground laying on back (140_08).fbx | 140_08 | Get Up From Ground Laying on Back | Getting Up From Ground |
| Sit down and get up (143_18).fbx | 143_18 | Sit Down And Get Up | General Subject Capture |
| Sit on stool and get up (143_19).fbx | 143_19 | Sit On Stool And Get Up | General Subject Capture |

## Stairs Steps Slopes (14)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Walk on uneven terrain (03_01).fbx | 03_01 | walk on uneven terrain | walk on uneven terrain |
| Climb 3 steps (13_35).fbx | 13_35 | climb 3 steps | various everyday behaviors |
| Slope 1 (74_14).fbx | 74_14 | slope 1 | kicks and walking on slopes |
| Very large sidestep to right (83_05).fbx | 83_05 | very large sidestep to right | steps |
| Medium step forward and down (83_08).fbx | 83_08 | medium step forward and down | steps |
| Short step forward and down (83_09).fbx | 83_09 | short step forward and down | steps |
| Short sidestep to left and down (83_14).fbx | 83_14 | short sidestep to left and down | steps |
| Medium step forward (83_20).fbx | 83_20 | medium step forward | steps |
| Medium step to left and up (83_24).fbx | 83_24 | medium step to left and up | steps |
| Short step forward and up (83_25).fbx | 83_25 | short step forward and up | steps |
| Walk forward and up stairs (83_34).fbx | 83_34 | walk forward and up stairs | steps |
| Walking up stairs (111_27).fbx | 111_27 | Walking up stairs | Pregnant Woman |
| Walk up and down stairs (113_20).fbx | 113_20 | Walk up and down stairs | Post pregnant woman |
| Varying height and length steps (122_01).fbx | 122_01 | clean | Varying Height and Length Steps |

## Walk (26)

| File | CMU trial | Motion | Subject |
|---|---|---|---|
| Walk (07_01).fbx | 07_01 | walk | walk |
| Walk, veer left (16_11).fbx | 16_11 | walk, veer left | run, jump, walk |
| Walk, veer right (16_13).fbx | 16_13 | walk, veer right | run, jump, walk |
| Walk, 90-degree left turn (16_17).fbx | 16_17 | walk, 90-degree left turn | run, jump, walk |
| Walk, 90-degree right turn (16_19).fbx | 16_19 | walk, 90-degree right turn | run, jump, walk |
| Slow walk, stop (16_33).fbx | 16_33 | slow walk, stop | run, jump, walk |
| Walk forward, turn around, walk back (36_02).fbx | 36_02 | walk forward, turn around, walk back | walk on uneven terrain |
| Navigate - walk forward, backward, sideways, on a diagonal (41_02).fbx | 41_02 | navigate - walk forward, backward, sideways, on a diagonal | navigate from corner to corner, interact with stepstool |
| Walk forward (69_01).fbx | 69_01 | walk forward | walking |
| Turn in place (69_16).fbx | 69_16 | turn in place | walking |
| Turn in place (opposite direction) (69_18).fbx | 69_18 | turn in place (opposite direction) | walking |
| Drag bad leg walk (91_25).fbx | 91_25 | DragBadLegWalk | walks and turns |
| Walking with obstacles 1 (107_01).fbx | 107_01 | (no description) | Walking with obstacles 1 |
| Walking with obstacles 2 (108_01).fbx | 108_01 | (no description) | Walking with obstacles 2 |
| Start walk stop (131_01).fbx | 131_01 | Start Walk Stop | Michael Jackson Styled Motions |
| Start walk left (131_09).fbx | 131_09 | Start Walk Left | Michael Jackson Styled Motions |
| Start walk right (131_12).fbx | 131_12 | Start Walk Right | Michael Jackson Styled Motions |
| Walk backwards (132_13).fbx | 132_13 | Walk Backwards | Varying Weird Walks |
| Walk fast (132_17).fbx | 132_17 | Walk Fast | Varying Weird Walks |
| Walk slow (132_45).fbx | 132_45 | Walk Slow | Varying Weird Walks |
| Walk stop walk (133_11).fbx | 133_11 | Walk Stop Walk | Baby Styled Walk |
| Walk left (133_14).fbx | 133_14 | Walk Left | Baby Styled Walk |
| Walk right (133_17).fbx | 133_17 | Walk Right | Baby Styled Walk |
| Normal walk backwards (136_25).fbx | 136_25 | Normal Walk Backwards | Weird Walks |
| Normal walk (137_29).fbx | 137_29 | Normal Walk | Stylized Motions |
| Walk wounded leg (139_19).fbx | 139_19 | Walk Wounded Leg | Action Walks, sneaking, wounded, looking around |
