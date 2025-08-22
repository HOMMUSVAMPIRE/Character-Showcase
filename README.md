# Character Showcase
This Unity project is intended to showcase my capabilities in the area of creating satisfying character controllers which feel responsive, satisfying, and intuitive. It is also meant to showcase my coding style for possible employers and demonstrate expertise with C# and Unity. Peruse at your own discretion.

This project is still a work in progress. I will be adding to it in my free time as I search for new employment.

Once the project meets my standards for completion, I may release it as a Unity Asset Store package for others to use in their games.

You can find my portfolio at [adalynnskidmore@myportfolio.com](https://adalynnskidmore.myportfolio.com/)

Please direct all questions, requests, and concerns to adalynnskidmore1@gmail.com

## How to open this Project
1. Clone the repo locally (more details here if needed: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository)
2. Download UnityHub from https://unity.com/download
3. Download and install Unity version 6000.0.50f1 via UnityHub or from https://unity.com/releases/editor/archive
4. Open UnityHub
5. Navigate to Add => Add project from disk
6. Select the location of the cloned repo from step 1
7. Open the project
8. Click "Play"

## How to see the code in this project
All of the code in this project is in the Assets/Scripts folder.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/Scripts/MovementModes/BasicMovementMode

If you intend to get a full understanding of the project, start with ACCharacterController.cs, then go through MovementController.cs, MovementMode.cs, and BasicMovementMode.cs (the latter two are in a subfolder of the main scripts folder) and from there, read through anything you like.

## Future Changes:
### High Priority Changes
- Add Camera System
- Prevent sliding on inclines (Bug exists because of current collision handling and prevention)
- Enable stairs traversal
- Add a dash that feels good
- Make character move with ground transform
- Fix collision bugs
- Add automated tests to run

### Medium Priority Changes
- Add basic character model and animations
- Add animation interface
- Tune performance
- Refactor for improved readability in some areas

### Low Priority Changes (these would be really cool but realistically I won't get to them for a while)
- Add Juice and polish
- Coordinate system re-centering to avoid floating point precision related bugs
- Add AI system which uses the same character scripts to pathfind and perform actions
