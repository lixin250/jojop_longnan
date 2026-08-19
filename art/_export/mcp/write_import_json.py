import json
from pathlib import Path

code = """
AssetDatabase.Refresh();
string[] paths = {
  "Assets/Bundle/Role/battle/role_lixin_battle.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_idle.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_walk.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_atk.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_hurt.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_taunt.png",
  "Assets/Bundle/Role/battle/role_lixin_battle_dead.png"
};
int n = 0;
foreach (var p in paths)
{
    var imp = AssetImporter.GetAtPath(p) as TextureImporter;
    if (imp == null) continue;
    imp.textureType = TextureImporterType.Sprite;
    imp.spriteImportMode = SpriteImportMode.Single;
    imp.alphaIsTransparency = true;
    imp.mipmapEnabled = false;
    var settings = new TextureImporterSettings();
    imp.ReadTextureSettings(settings);
    settings.spriteAlignment = (int)SpriteAlignment.Custom;
    settings.spritePivot = new Vector2(0.5f, 0.12f);
    imp.SetTextureSettings(settings);
    imp.SaveAndReimport();
    n++;
}
Debug.Log("[Art] sprite-imported " + n);
"""

obj = {"csharpCode": code.strip(), "isMethodBody": True}
p = Path(r"e:\Project\JojoP\art\_export\mcp\import_sprites.json")
p.write_text(json.dumps(obj, ensure_ascii=False), encoding="utf-8")
print("wrote", p)
