import json
from pathlib import Path

d = Path(r"e:/Project/JojoP/art/_export/mcp")
d.mkdir(parents=True, exist_ok=True)

def w(name, obj):
    (d / name).write_text(json.dumps(obj, ensure_ascii=False), encoding="utf-8")

w("empty.json", {})
w("play_off.json", {"isPlaying": False, "isPaused": False})
w("play_on.json", {"isPlaying": True, "isPaused": False})
w("open_bootstrap.json", {
    "sceneRef": {"assetPath": "Assets/Scenes/Bootstrap.unity"},
    "loadSceneMode": "Single",
})
w("err.json", {"maxEntries": 40, "logTypeFilter": "Error", "includeStackTrace": False, "lastMinutes": 15})
w("ex.json", {"maxEntries": 20, "logTypeFilter": "Exception", "includeStackTrace": True, "lastMinutes": 15})

w("dump_btns.json", {
    "className": "McpDump",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using System.Text;
using UnityEngine;
using UnityEngine.UI;
public static class McpDump {
  public static string Run() {
    var sb = new StringBuilder();
    foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
      if (!b.gameObject.activeInHierarchy) continue;
      var t = b.GetComponentInChildren<Text>();
      string label = t != null ? t.text.Replace("\\n", " ") : "";
      sb.Append(b.name).Append(" | ").Append(label).Append(" ; ");
    }
    return sb.Length == 0 ? "NO_BUTTONS" : sb.ToString();
  }
}
""",
})

w("click_brothers.json", {
    "className": "McpClick",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using UnityEngine;
using UnityEngine.UI;
public static class McpClick {
  public static string Run() {
    foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
      if (!b.gameObject.activeInHierarchy) continue;
      if (b.name == "btn_brothers") { b.onClick.Invoke(); return "clicked btn_brothers"; }
    }
    return "btn_brothers missing";
  }
}
""",
})

w("click_hero0.json", {
    "className": "McpHero",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using UnityEngine;
using UnityEngine.UI;
public static class McpHero {
  public static string Run() {
    foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
      if (!b.gameObject.activeInHierarchy) continue;
      if (b.name == "Hero0") { b.onClick.Invoke(); return "clicked Hero0"; }
    }
    return "Hero0 missing";
  }
}
""",
})

w("click_select.json", {
    "className": "McpSelect",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using UnityEngine;
using UnityEngine.UI;
public static class McpSelect {
  public static string Run() {
    foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
      if (!b.gameObject.activeInHierarchy) continue;
      if (b.name == "Select") { b.onClick.Invoke(); return "clicked Select"; }
    }
    return "Select missing";
  }
}
""",
})

w("click_start.json", {
    "className": "McpStart",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using UnityEngine;
using UnityEngine.UI;
public static class McpStart {
  public static string Run() {
    foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
      if (!b.gameObject.activeInHierarchy) continue;
      if (b.name == "BtnStart") { b.onClick.Invoke(); return "clicked BtnStart"; }
    }
    return "BtnStart missing";
  }
}
""",
})

w("open_gameview.json", {
    "isMethodBody": True,
    "csharpCode": """
var t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
if (t == null) { Debug.LogError("no GameView type"); return; }
var w = EditorWindow.GetWindow(t);
w.Show();
w.Focus();
Debug.Log("[JojoP] Game View opened");
""",
})

w("capture_play.json", {
    "className": "CapturePlay",
    "methodName": "Run",
    "isMethodBody": False,
    "csharpCode": """
using System.IO;
using UnityEngine;
public static class CapturePlay {
  public static string Run() {
    var cam = Camera.main;
    if (cam == null) return "no camera";
    int w = 540, h = 960;
    var rt = new RenderTexture(w, h, 24);
    var prev = cam.targetTexture;
    cam.targetTexture = rt;
    cam.Render();
    RenderTexture.active = rt;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
    tex.Apply();
    cam.targetTexture = prev;
    RenderTexture.active = null;
    Object.DestroyImmediate(rt);
    var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/_export/mcp"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "qa_cam.png");
    File.WriteAllBytes(path, tex.EncodeToPNG());
    Object.DestroyImmediate(tex);
    return path;
  }
}
""",
})

print("ok")
