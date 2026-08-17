#!/usr/bin/env python3
"""从概念总图按 layout JSON 裁切，输出 art/_final/{role_id}/ 供 Unity 覆盖导入。

仅依赖 Pillow（本机 Python 3.14 的 numpy/rembg 不可用时仍可跑）。
羊皮纸底用颜色近似抠透明；带框头像/技能图默认不抠，保留框内底。

用法:
  python art/tools/crop_concept_sheet.py
  python art/tools/crop_concept_sheet.py --layout art/layouts/oban_gpt_eu.json
  python art/tools/crop_concept_sheet.py --no-matte
"""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art"


def parchment_matte(im: Image.Image, sample_corners: bool = True, threshold: int = 42) -> Image.Image:
    """把接近羊皮纸底色的像素变透明。

    注意：本机 Python 3.14 + Pillow 下 Image.load() 会崩，改用 getdata/putdata。
    """
    rgba = im.convert("RGBA")
    w, h = rgba.size
    # 本机 Pillow + 3.14：避免 load()；get_flattened_data 打包格式不稳定，固定用 getdata
    raw = list(rgba.getdata())
    data = []
    for px in raw:
        if isinstance(px, tuple) and len(px) >= 4:
            data.append((int(px[0]), int(px[1]), int(px[2]), int(px[3])))
        elif isinstance(px, (int, float)):
            # 不应出现；兜底跳过
            continue
        else:
            try:
                data.append((int(px[0]), int(px[1]), int(px[2]), int(px[3]) if len(px) > 3 else 255))
            except Exception:
                continue
    if len(data) != w * h:
        # 回退：不抠图
        return rgba

    def at(x: int, y: int):
        return data[y * w + x]

    samples = []
    if sample_corners:
        for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3), (w // 2, 2), (2, h // 2)):
            if not (0 <= x < w and 0 <= y < h):
                continue
            r, g, b, a = at(x, y)
            if r > 160 and g > 140 and b > 100 and r >= b:
                samples.append((r, g, b))
    if not samples:
        samples = [(210, 190, 150)]

    sr = sum(c[0] for c in samples) // len(samples)
    sg = sum(c[1] for c in samples) // len(samples)
    sb = sum(c[2] for c in samples) // len(samples)
    thr2 = threshold * threshold * 3

    out_data = []
    for r, g, b, a in data:
        dr, dg, db = r - sr, g - sg, b - sb
        if dr * dr + dg * dg + db * db <= thr2:
            out_data.append((r, g, b, 0))
        else:
            out_data.append((r, g, b, a))

    out = Image.new("RGBA", (w, h))
    out.putdata(out_data)
    return out


def fit_square(im: Image.Image, size: int) -> Image.Image:
    """等比放入 size×size，透明垫底居中。"""
    im = im.convert("RGBA")
    w, h = im.size
    scale = min(size / w, size / h)
    nw, nh = max(1, int(round(w * scale))), max(1, int(round(h * scale)))
    resized = im.resize((nw, nh), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(resized, ((size - nw) // 2, (size - nh) // 2), resized)
    return canvas


def process_layout(layout_path: Path, do_matte: bool, write_export: bool) -> Path:
    data = json.loads(layout_path.read_text(encoding="utf-8"))
    role_id = data["role_id"]
    source = ART / data["source"]
    if not source.is_file():
        raise FileNotFoundError(f"找不到源图: {source}")

    src = Image.open(source).convert("RGBA")
    sw, sh = src.size
    expected = data.get("source_size")
    if expected and list(expected) != [sw, sh]:
        print(f"[warn] 源图尺寸 {sw}x{sh} 与 layout 标定 {expected} 不一致，仍按绝对像素裁。")

    export_dir = ART / "_export" / role_id
    final_dir = ART / "_final" / role_id
    if export_dir.exists():
        shutil.rmtree(export_dir)
    if final_dir.exists():
        shutil.rmtree(final_dir)
    export_dir.mkdir(parents=True, exist_ok=True)
    final_dir.mkdir(parents=True, exist_ok=True)

    manifest = []
    for crop in data["crops"]:
        key = crop["key"]
        l, t, r, b = crop["box"]
        l, t = max(0, l), max(0, t)
        r, b = min(sw, r), min(sh, b)
        piece = src.crop((l, t, r, b))

        matte_mode = crop.get("matte", "none") if do_matte else "none"
        if matte_mode == "parchment":
            piece = parchment_matte(piece)

        if write_export:
            piece.save(export_dir / f"{key}.png")

        out = piece
        resize = crop.get("resize")
        if resize:
            out = fit_square(piece, int(resize[0]))

        unity_name = crop["unity_name"]
        unity_subdir = crop["unity_subdir"].replace("\\", "/")
        out_path = final_dir / unity_name
        out.save(out_path)

        manifest.append(
            {
                "key": key,
                "file": unity_name,
                "unity_subdir": unity_subdir,
                "matte": matte_mode,
                "size": list(out.size),
            }
        )
        print(f"  {key:20} -> {unity_subdir}/{unity_name}  {out.size}")

    man_path = final_dir / "manifest.json"
    man_path.write_text(
        json.dumps(
            {
                "role_id": role_id,
                "source": data["source"],
                "layout": str(layout_path.relative_to(ROOT)).replace("\\", "/"),
                "items": manifest,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"[ok] _final/{role_id}/ 共 {len(manifest)} 张，manifest={man_path}")
    return final_dir


def main() -> None:
    ap = argparse.ArgumentParser(description="概念总图裁切 → art/_final")
    ap.add_argument(
        "--layout",
        type=Path,
        default=None,
        help="单个 layout JSON；省略且不加 --all 时默认欧版",
    )
    ap.add_argument(
        "--all",
        action="store_true",
        help="处理 art/layouts 下全部 *.json",
    )
    ap.add_argument("--no-matte", action="store_true", help="关闭羊皮纸抠透明")
    ap.add_argument("--no-export", action="store_true", help="不写 _export 草稿")
    args = ap.parse_args()

    layouts: list[Path]
    if args.all:
        layouts = sorted((ART / "layouts").glob("*.json"))
    elif args.layout is not None:
        layouts = [args.layout if args.layout.is_absolute() else ROOT / args.layout]
    else:
        layouts = [ART / "layouts" / "oban_gpt_eu.json"]

    if not layouts:
        raise SystemExit("没有找到 layout JSON")

    for layout in layouts:
        print(f"\n=== layout: {layout} ===")
        process_layout(layout, do_matte=not args.no_matte, write_export=not args.no_export)


if __name__ == "__main__":
    main()
