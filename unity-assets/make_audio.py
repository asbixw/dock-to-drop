#!/usr/bin/env python3
"""Compatibility entry point for the game's sound-effect build.

The old synthetic generator (square/saw tones and filtered noise) was the source
of the harsh buzz and tick-like footsteps.  All effects are now cut from real
CC0 field recordings by make_sfx_natural.py; run this file or that one, the
result is the same.
"""

import os
import runpy

if __name__ == "__main__":
    runpy.run_path(
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "make_sfx_natural.py"),
        run_name="__main__",
    )
