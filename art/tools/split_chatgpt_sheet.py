#!/usr/bin/env python3
"""把 ChatGPT 动作总图拆成 idle/walk/atk/… 512 帧，写入 cache 并拷进 Bundle。"""
from __future__ import annotations

import shutil
import sys
import uuid
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from minimax_pose import (  # noqa: E402
    REPO,
    content_bbox,
    maybe_knock_bg,
    normalize_frames,
)

SHEET = REPO / "art" / "pose" / "chatgpt" / "lixin_pose.png"
CACHE = REPO / "art" / "pose" / ".cache" / "lixin"
BUNDLE = REPO / "unity" / "Assets" / "Bundle" / "Role" / "lixin" / "battle"
META_TMPL = BUNDLE / "idle.png.meta"
TOP = ("idle", "walk", "atk")
BOT = ("skill", "hurt", "dead", "taunt")
CANVAS = (512, 512)
GROUND = 0.12


def occupied(im: Image.Image) -> tuple[list[int], list[int]]:
    rgba = im.convert("RGBA")
    w, h = rgba.size
    data = list(rgba.getdata())
    col = [0] * w
    row = [0] * h
    for i, px in enumerate(data):
        if px[3] > 24 and px[0] + px[1] + px[2] > 50:
            col[i % w] += 1
            row[i // w] += 1
    return col, row


def bands(fill: list[int], min_span: int, thresh: int) -> list[tuple[int, int]]:
    out: list[tuple[int, int]] = []
    i = 0
    n = len(fill)
    while i < n:
        if fill[i] >= thresh:
            j = i
            while j < n and fill[j] >= thresh:
                j += 1
            if j - i >= min_span:
                out.append((i, j))
            i = j
        else:
            i += 1
    return out


def split_on_valley(col: list[int], left: int, right: int) -> int:
    lo = left + int((right - left) * 0.35)
    hi = left + int((right - left) * 0.65)
    if hi <= lo + 4:
        return (left + right) // 2
    seg = col[lo:hi]
    return lo + min(range(len(seg)), key=lambda k: seg[k])


def blobs(col: list[int], want: int, yspan: int) -> list[tuple[int, int]]:
    thresh = max(4, int(yspan * 0.02))
    found = bands(col, 8, thresh)
    found.sort(key=lambda b: b[0])
    while len(found) < want:
        wide_i = max(range(len(found)), key=lambda i: found[i][1] - found[i][0])
        l, r = found[wide_i]
        if r - l < 40:
            break
        mid = split_on_valley(col, l, r)
        found = found[:wide_i] + [(l, mid), (mid, r)] + found[wide_i + 1 :]
        found.sort(key=lambda b: b[0])
    if len(found) > want:
        found.sort(key=lambda b: b[1] - b[0], reverse=True)
        found = sorted(found[:want], key=lambda b: b[0])
    pad = 8
    w = len(col)
    return [(max(0, l - pad), min(w, r + pad)) for l, r in found]


def write_meta(dest: Path) -> None:
    if dest.with_suffix(".png.meta").exists() if dest.suffix != ".png" else dest.with_name(dest.name + ".meta").exists():
        pass
    meta_path = Path(str(dest) + ".meta") if dest.suffix != ".png" else dest.with_suffix(".png.meta")
    # dest is foo.png → foo.png.meta
    meta_path = dest.with_name(dest.name + ".meta")
    if meta_path.is_file():
        return
    tmpl = META_TMPL.read_text(encoding="utf-8")
    guid = uuid.uuid4().hex
    sprite_id = uuid.uuid4().hex[:16] + "0800000000000000"
    text = tmpl.replace("guid: ae4b93b17916f58469791d9cde726e69", f"guid: {guid}")
    text = text.replace("spriteID: 5e97eb03825dee720800000000000000", f"spriteID: {sprite_id}")
    meta_path.write_text(text, encoding="utf-8")


def faces_right(im: Image.Image) -> bool:
    """Rough: more opaque mass in the left half of the bbox → facing right (head/hands forward). Unreliable; we also check atk VFX."""
    box = content_bbox(im)
    if box is None:
        return True
    l, t, r, b = box
    rgba = im.convert("RGBA")
    data = list(rgba.getdata())
    w, h = rgba.size
    mid = (l + r) // 2
    left = right = 0
    for y in range(t, b):
        row = y * w
        for x in range(l, r):
            p = data[row + x]
            if p[3] > 24:
                if x < mid:
                    left += 1
                else:
                    right += 1
    return right >= left


def main() -> None:
    if not SHEET.is_file():
        raise SystemExit(f"找不到 {SHEET}")
    sheet = maybe_knock_bg(Image.open(SHEET).convert("RGBA"))
    w, h = sheet.size
    col, row = occupied(sheet)
    rows = bands(row, 24, max(4, int(w * 0.01)))
    if len(rows) < 2:
        rows = [(0, h // 2), (h // 2, h)]
    top_y = rows[0]
    bot_y = rows[1]
    print("rows", top_y, bot_y, "sheet", (w, h), flush=True)

    def slice_row(y0: int, y1: int, names: tuple[str, ...]) -> list[tuple[str, Image.Image]]:
        band = sheet.crop((0, y0, w, y1))
        c, _ = occupied(band)
        xs = blobs(c, len(names), y1 - y0)
        if names == BOT and len(xs) >= 4:
            xs[2] = (xs[2][0], 1172)
            xs[3] = (1240, xs[3][1])
        out = []
        for name, (x0, x1) in zip(names, xs):
            cell = band.crop((x0, 0, x1, y1 - y0))
            print(" ", name, "x", x0, x1, "size", cell.size, flush=True)
            out.append((name, cell))
        return out

    cells = slice_row(top_y[0], top_y[1], TOP) + slice_row(bot_y[0], bot_y[1], BOT)
    CACHE.mkdir(parents=True, exist_ok=True)
    BUNDLE.mkdir(parents=True, exist_ok=True)

    # Shared scale from idle (index 0).
    packed = normalize_frames([c for _, c in cells], CANVAS, GROUND, key_index=0)
    named = list(zip([n for n, _ in cells], packed))

    # Attack VFX should sit to the facing side. If atk bbox extends opposite of idle, don't flip blindly.
    idle = next(im for n, im in named if n == "idle")
    print("idle faces_right heuristic", faces_right(idle), flush=True)

    for name, im in named:
        raw = CACHE / f"_sheet_{name}.png"
        dest_cache = CACHE / f"{name}_1.png"
        im.save(raw, format="PNG")
        im.save(dest_cache, format="PNG")
        for unity_name in (f"{name}_1.png", f"{name}.png"):
            dest = BUNDLE / unity_name
            im.save(dest, format="PNG")
            write_meta(dest)
            print("→", dest.relative_to(REPO).as_posix(), im.size, dest.stat().st_size, flush=True)
        fallback = BUNDLE / "fallback.png"
        if name == "idle":
            im.save(fallback, format="PNG")


if __name__ == "__main__":
    main()
