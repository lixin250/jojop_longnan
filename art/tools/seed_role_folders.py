#!/usr/bin/env python3
"""Fill Assets/Bundle/Role/{artId}/ from config loc + existing portraits. Missing slots get placeholders."""
from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parents[2]
BUNDLE = REPO / "unity" / "Assets" / "Bundle"
ROLE_ROOT = BUNDLE / "Role"
ITEM_ICON = BUNDLE / "Item" / "icon"
FINAL = REPO / "art" / "_final"
OLD = ROLE_ROOT / "oldAvatar"
FONT = Path("C:/Windows/Fonts/msyh.ttc")

# art folder = RoleArtLoader.RoleFolder(avatar_loc)
ROLES = [
    ("lixin", "猩哥", (70, 110, 170)),
    ("ayun", "阿允", (90, 140, 190)),
    ("xiebo", "谢博", (160, 90, 70)),
    ("oban", "欧版", (80, 130, 100)),
    ("wuzi", "物资", (70, 150, 130)),
    ("jihong", "红双喜", (180, 70, 80)),
    ("laowang", "侯哥", (90, 90, 140)),
    ("hangu", "含固", (180, 150, 60)),
    ("xiaolin", "陈大夫", (70, 150, 110)),
    ("dacheng", "鱼固", (140, 110, 70)),
    ("wantao", "万总", (150, 120, 90)),
    ("junjun", "军军", (100, 120, 90)),
    ("paopao", "炮炮", (170, 90, 50)),
    ("xiaogu", "肖医师", (80, 140, 150)),
    ("laizhen", "赖镇", (150, 80, 120)),
    ("longtou", "龙头", (120, 80, 60)),
    ("engshadow", "机械影", (90, 90, 90)),
    ("tempcitizen", "热心市民", (140, 120, 100)),
    ("temphire", "临时工", (110, 110, 80)),
]

BATTLE_SLOTS = ("idle", "walk", "atk", "hurt", "dead", "fallback")

KIND_ICONS = {
    "stat": ((210, 120, 60), "力"),
    "recovery": ((70, 160, 90), "贴"),
    "teambuff": ((200, 80, 80), "队"),
    "campusskill": ((80, 130, 190), "课"),
    "jobskill": ((180, 150, 50), "工"),
    "encounter": ((150, 90, 160), "遇"),
    "equipment": ((90, 100, 140), "核"),
    "lootskill": ((160, 110, 70), "摊"),
    "event": ((180, 70, 90), "事"),
}


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    if FONT.exists():
        return ImageFont.truetype(str(FONT), size)
    return ImageFont.load_default()


def first_existing(paths: list[Path]) -> Path | None:
    for p in paths:
        if p.is_file():
            return p
    return None


def load_rgba(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    return im


def trim_label(im: Image.Image) -> Image.Image:
    """Drop concept-sheet captions sitting above a framed portrait."""
    w, h = im.size
    if h < 80:
        return im
    # Average brightness of top 18% vs the rest — if top is much lighter parchment+text, crop it.
    top = im.crop((0, 0, w, max(1, int(h * 0.2)))).convert("L")
    rest = im.crop((0, int(h * 0.22), w, h)).convert("L")
    if _mean(top) > _mean(rest) + 18:
        cropped = im.crop((int(w * 0.04), int(h * 0.20), int(w * 0.96), int(h * 0.98)))
        return cropped
    return im


def _mean(im: Image.Image) -> float:
    hist = im.histogram()
    total = sum(hist) or 1
    acc = sum(i * v for i, v in enumerate(hist))
    return acc / total


def round_head(avatar: Image.Image, size: int) -> Image.Image:
    head = avatar.resize((size, size), Image.Resampling.LANCZOS).convert("RGBA")
    plate = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(plate)
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=max(8, size // 6), fill=(255, 255, 255, 255))
    _, _, _, mask = plate.split()
    head.putalpha(mask)
    return head


def placeholder_avatar(name: str, color: tuple[int, int, int], size: int = 256) -> Image.Image:
    src = first_existing([FINAL / "lixin" / "avatar.png", ROLE_ROOT / "lixin" / "avatar.png"])
    if src is not None:
        im = load_rgba(src).resize((size, size), Image.Resampling.LANCZOS)
        overlay = Image.new("RGBA", (size, size), (*color, 80))
        return Image.alpha_composite(im.convert("RGBA"), overlay)
    return Image.new("RGBA", (size, size), (*color, 255))


def battle_pose(avatar: Image.Image, color: tuple[int, int, int], pose: str) -> Image.Image:
    w, h = 220, 280
    im = Image.new("RGBA", (w, h), (*color, 255))
    size = 148 if pose != "dead" else 120
    head = avatar.resize((size, size), Image.Resampling.LANCZOS).convert("RGBA")
    x = (w - size) // 2 + (14 if pose == "atk" else (-10 if pose == "hurt" else 0))
    y = 24 if pose == "atk" else (48 if pose == "hurt" else (110 if pose == "dead" else 36))
    if pose == "walk":
        y = 30
        x += 8
    im.paste(head, (x, y), head)
    return im


def save_png(im: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, "PNG")


def copy_or_convert(src: Path, dst: Path, trim: bool = False) -> None:
    im = load_rgba(src)
    if trim:
        im = trim_label(im)
    save_png(im, dst)


def seed_role(folder: str, name: str, color: tuple[int, int, int]) -> None:
    print("  dest", folder, flush=True)
    dest = ROLE_ROOT / folder
    dest.mkdir(parents=True, exist_ok=True)
    battle = dest / "battle"
    battle.mkdir(exist_ok=True)
    print("  find avatar", flush=True)

    avatar_src = first_existing(
        [
            FINAL / folder / "avatar.png",
            FINAL / folder / f"role_{folder}_avatar.png",
            dest / "avatar.png",
            OLD / f"role_{folder}_avatar.png",
            OLD / f"role_{folder}_avatar.jpg",
        ]
    )
    if avatar_src is not None:
        print("  copy avatar", avatar_src, flush=True)
        copy_or_convert(avatar_src, dest / "avatar.png", trim=True)
    else:
        print("  placeholder avatar", flush=True)
        save_png(placeholder_avatar(name, color), dest / "avatar.png")

    print("  load avatar", flush=True)
    avatar = load_rgba(dest / "avatar.png")
    print("  avatar ok", avatar.size, flush=True)

    print("  half", flush=True)
    half_src = first_existing(
        [
            FINAL / folder / "half.png",
            FINAL / folder / f"role_{folder}_half.png",
            FINAL / folder / f"role_{folder}_half_worker.png",
            dest / "half.png",
        ]
    )
    if half_src is not None:
        copy_or_convert(half_src, dest / "half.png", trim=True)
    else:
        save_png(avatar.resize((256, 320), Image.Resampling.LANCZOS), dest / "half.png")

    print("  poster", flush=True)
    poster_src = first_existing(
        [
            FINAL / folder / "poster.png",
            FINAL / folder / f"role_{folder}_poster.png",
            dest / "poster.png",
        ]
    )
    if poster_src is not None:
        copy_or_convert(poster_src, dest / "poster.png", trim=True)
    else:
        poster = Image.new("RGBA", (280, 420), (0, 0, 0, 0))
        p = avatar.resize((240, 240), Image.Resampling.LANCZOS)
        poster.paste(p, (20, 40), p)
        save_png(poster, dest / "poster.png")

    banner_src = first_existing([FINAL / folder / "banner.png", FINAL / folder / f"role_{folder}_banner.png", dest / "banner.png"])
    if banner_src is not None:
        copy_or_convert(banner_src, dest / "banner.png", trim=True)

    print("  battle", flush=True)
    keep_existing_battle = folder == "lixin"
    final_battle = {
        "idle": first_existing(
            [
                FINAL / folder / "idle.png",
                FINAL / folder / f"role_{folder}_battle_idle.png",
                FINAL / folder / f"role_{folder}_battle_student.png",
            ]
        ),
        "walk": first_existing([FINAL / folder / "walk.png", FINAL / folder / f"role_{folder}_battle_walk.png"]),
        "atk": first_existing(
            [
                FINAL / folder / "atk.png",
                FINAL / folder / f"role_{folder}_battle_atk.png",
                FINAL / folder / f"role_{folder}_battle_grad.png",
            ]
        ),
        "hurt": first_existing([FINAL / folder / "hurt.png", FINAL / folder / f"role_{folder}_battle_hurt.png"]),
        "dead": first_existing([FINAL / folder / "dead.png", FINAL / folder / f"role_{folder}_battle_dead.png"]),
        "fallback": first_existing([FINAL / folder / "fallback.png", FINAL / folder / f"role_{folder}_battle.png"]),
    }

    for slot in BATTLE_SLOTS:
        dst = battle / f"{slot}.png"
        src = final_battle.get(slot)
        print("    slot", slot, src, flush=True)
        if keep_existing_battle and dst.exists():
            continue
        if src is not None and src.is_file():
            copy_or_convert(src, dst)
        else:
            tmp = REPO / "art" / "_export" / "seed_roles" / folder / f"{slot}.png"
            save_png(battle_pose(avatar, color, slot), tmp)
            save_png(Image.open(tmp).convert("RGBA"), dst)
            print("    wrote", slot, flush=True)

    taunt_src = first_existing([dest / "battle" / "taunt.png", FINAL / folder / "taunt.png", FINAL / folder / f"role_{folder}_battle_taunt.png"])
    if taunt_src is not None and taunt_src.is_file():
        copy_or_convert(taunt_src, battle / "taunt.png")


def seed_rogue_icons() -> None:
    ITEM_ICON.mkdir(parents=True, exist_ok=True)
    for kind, (color, _glyph) in KIND_ICONS.items():
        im = Image.new("RGBA", (256, 320), (*color, 255))
        inner = Image.new("RGBA", (200, 200), (24, 22, 20, 255))
        im.paste(inner, (28, 36))
        save_png(im, ITEM_ICON / f"rogue_{kind}.png")


def main() -> None:
    for folder, name, color in ROLES:
        print("seed", folder, flush=True)
        try:
            seed_role(folder, name, color)
        except Exception as e:
            print("FAIL", folder, type(e).__name__, e, flush=True)
            raise
    seed_rogue_icons()
    print("rogue icons ok")


if __name__ == "__main__":
    main()
