import json
from pathlib import Path

p = Path(r"e:\Project\JojoP\art\_export\mcp\logs_info.json")
p.write_text(json.dumps({
    "maxEntries": 30,
    "logTypeFilter": "Log",
    "includeStackTrace": False,
    "lastMinutes": 3
}, ensure_ascii=False), encoding="utf-8")
