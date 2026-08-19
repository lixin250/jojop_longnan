import base64
import sys
from pathlib import Path

src = Path(sys.argv[1])
dst = Path(sys.argv[2])
text = src.read_bytes()
if text.startswith(b"\xff\xfe") or text.startswith(b"\xfe\xff"):
    text = text.decode("utf-16")
else:
    text = text.decode("utf-8", errors="replace")
key = "iVBORw0KGgo"
i = text.find(key)
if i < 0:
    print("no png")
    sys.exit(1)
j = text.find('"', i)
b64 = text[i:j]
dst.write_bytes(base64.b64decode(b64))
print("wrote", dst, dst.stat().st_size, "b64", len(b64))
