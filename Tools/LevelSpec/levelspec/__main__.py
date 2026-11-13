"""Runs the command line: python3 Tools/LevelSpec/levelspec ... (or python3 -m levelspec from Tools/LevelSpec)."""

import os
import sys

if __package__ in (None, ""):
    # Run as a folder (python3 Tools/LevelSpec/levelspec): import the package from beside it.
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from levelspec.cli import main
else:
    from .cli import main

sys.exit(main())
