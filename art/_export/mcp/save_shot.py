import json
import re
import base64
from pathlib import Path

raw = Path(r"C:\Users\Administrator\.cursor\projects\e-Project-JojoP\agent-tools\81df0cc8-efd8-4edd-8f5b-1b0449b6fef2.txt").read_text(encoding="utf-8")
m = re.search(r'"data": "(iVBORw0KGgo[^"]+)"', raw)
print("found", bool(m), "len", len(m.group(1)) if m else 0)
if m:
    out = Path(r"e:\Project\JojoP\art\_export\mcp\shot_play.png")
    out.write_bytes(base64.b64decode(m.group(1)))
    print("wrote", out, out.stat().st_size)
