import json
from pathlib import Path

code = """var setting = YooAsset.Editor.BundleCollectorSettingData.Setting;
var result = setting.BeginCollect("DefaultPackage", true, false);
UnityEngine.Debug.Log("[JojoP] collect ok count=" + result.CollectAssets.Count);
"""

payload = {
    "isMethodBody": True,
    "csharpCode": code,
}
Path(r"e:/Project/JojoP/art/_export/mcp/collect.json").write_text(
    json.dumps(payload, ensure_ascii=False), encoding="utf-8"
)
print("wrote collect.json")
