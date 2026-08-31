#!/usr/bin/env python3
"""抽出丢给 ChatGPT 的利欣包：身份锁、概念图动作裁切、512 落地图。"""
from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art"
OUT = ART / "pose" / "chatgpt_lixin"
BATTLE_KEYS = ("battle_idle", "battle_walk", "battle_atk", "battle_hurt", "battle_taunt", "battle_dead")


def main() -> None:
    book = json.loads((ART / "pose" / "poses.json").read_text(encoding="utf-8"))
    layout = json.loads((ART / "layouts" / "lixin_gpt.json").read_text(encoding="utf-8"))
    spec = next(c for c in book["characters"] if c["who"] == "lixin")
    OUT.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ART / spec["lock_ref"], OUT / "01_身份锁.png")
    shutil.copy2(ART / spec["lock"], OUT / "02_三视图.png")

    src = Image.open(ART / layout["source"]).convert("RGB")
    crops = {c["key"]: c for c in layout["crops"]}
    for key in BATTLE_KEYS:
        item = crops[key]
        box = tuple(int(x) for x in item["box"])
        name = (item.get("unity_name") or key).replace(".png", "")
        dest = OUT / f"03_概念图_{name}.png"
        src.crop(box).save(dest)
        print("crop", dest.name, src.crop(box).size)

    canvas = Image.new("RGB", (512, 512), (24, 24, 28))
    ref = Image.open(ART / spec["lock_ref"]).convert("RGB")
    h = 400
    w = max(1, int(ref.size[0] * h / ref.size[1]))
    ref = ref.resize((w, h), Image.Resampling.LANCZOS)
    x = (512 - w) // 2
    y = 512 - int(round(512 * 0.12)) - h
    canvas.paste(ref, (x, y))
    canvas.save(OUT / "00_画布框.png")
    print("frame", (x, y, w, h))


if __name__ == "__main__":
    main()
