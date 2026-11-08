#!/usr/bin/env python3
"""Compatibility entry point for dialogue generation.

The shipped macOS `say` voices were replaced with Edge neural voices.  This
wrapper exists so older notes that mention `voice/generate.py` still do the
right thing; the real generator is `generate_natural.py`.
"""

import os
import runpy

if __name__ == "__main__":
    runpy.run_path(
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "generate_natural.py"),
        run_name="__main__",
    )
