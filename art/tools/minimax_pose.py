#!/usr/bin/env python3
"""大底裁无字正面当身份锁。MiniMax：锁图+短prompt出候选，每动作一张。OpenAI：key 再补间。

  python art/tools/minimax_pose.py lock --who lixin
  python art/tools/minimax_pose.py gen --who lixin --poses idle
  python art/tools/minimax_pose.py pack --who lixin --poses idle --cand 2
  python art/tools/minimax_pose.py import --who lixin
"""
from __future__ import annotations

import argparse
import base64
import io
import json
import os
import shutil
from pathlib import Path

import requests
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
ART = REPO / "art"
POSE_DIR = ART / "pose"
SPECS = POSE_DIR / "poses.json"
CACHE = POSE_DIR / ".cache"
LOCKS = POSE_DIR / "locks"
VOICE_ENV = ART / "voice" / "secrets.env"
DEFAULT_STYLE = ART / "概念图" / "风格锁.txt"
PARCHMENT = (214, 196, 160)


def load_env() -> dict[str, str]:
    env = dict(os.environ)
    if VOICE_ENV.is_file():
        for raw in VOICE_ENV.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            env.setdefault(k.strip(), v.strip())
    return env


def cfg() -> tuple[str, str, str, str]:
    env = load_env()
    key = env.get("MINIMAX_API_KEY", "").strip()
    if not key:
        raise SystemExit("先填 art/voice/secrets.env 里的 MINIMAX_API_KEY")
    group = env.get("MINIMAX_GROUP_ID", "").strip()
    base = env.get("MINIMAX_BASE_URL", "https://api.minimax.chat").rstrip("/")
    data = json.loads(SPECS.read_text(encoding="utf-8"))
    model = env.get("MINIMAX_IMAGE_MODEL", "").strip() or data.get("minimax_model") or data.get("model") or "image-01"
    if str(model).startswith("gpt-"):
        model = "image-01"
    return key, group, base, model


def pose_provider(book: dict) -> str:
    env = load_env()
    return (env.get("POSE_PROVIDER") or book.get("provider") or "minimax").strip().lower()


def openai_cfg(book: dict) -> tuple[str, str, str, str, str]:
    env = load_env()
    key = env.get("OPENAI_API_KEY", "").strip()
    if not key:
        raise SystemExit(
            "换成 OpenAI 后，在 art/voice/secrets.env 填 OPENAI_API_KEY 即可。"
            "控制台：https://platform.openai.com/api-keys"
            "；GPT Image 可能还要组织验证。"
        )
    base = (env.get("OPENAI_BASE_URL") or "https://api.openai.com/v1").rstrip("/")
    model = env.get("OPENAI_IMAGE_MODEL") or book.get("openai_model") or "gpt-image-2"
    quality = env.get("OPENAI_IMAGE_QUALITY") or book.get("quality") or "medium"
    size = book.get("size") or "1536x1024"
    return key, base, model, quality, size


def api_url(base: str, path: str, group: str) -> str:
    u = f"{base}{path}"
    if group:
        sep = "&" if "?" in u else "?"
        u = f"{u}{sep}GroupId={group}"
    return u


def raise_api(resp: requests.Response) -> dict:
    try:
        data = resp.json()
    except Exception as e:
        raise SystemExit(f"非 JSON {resp.status_code}: {resp.text[:400]}") from e
    base = data.get("base_resp") or {}
    code = base.get("status_code", 0)
    if resp.status_code >= 400 or (code not in (0, None) and code != 0):
        raise SystemExit(f"MiniMax 错误 {resp.status_code} {base}: {json.dumps(data, ensure_ascii=False)[:800]}")
    return data


def load_book() -> dict:
    if not SPECS.is_file():
        raise SystemExit(f"找不到 {SPECS}")
    return json.loads(SPECS.read_text(encoding="utf-8"))


def character(book: dict, who: str) -> dict:
    for spec in book.get("characters") or []:
        if spec.get("who") == who:
            return spec
    raise SystemExit(f"poses.json 没有角色 {who}，先加一条或 --who 对上")


def pose_catalog(book: dict) -> dict[str, dict]:
    poses = book.get("poses") or {}
    if not poses:
        raise SystemExit("poses.json 没有 poses")
    return poses


def clip_frames(clip: dict) -> list[dict]:
    frames = clip.get("frames")
    if isinstance(frames, list) and frames:
        return frames
    fname = clip.get("file") or ""
    return [{"file": fname or "frame.png", "prompt": clip.get("prompt") or "", "note": clip.get("prompt") or ""}]


def pick_clips(book: dict, names: str | None) -> list[tuple[str, dict]]:
    catalog = pose_catalog(book)
    if not names:
        return list(catalog.items())
    wanted = {n.strip() for n in names.split(",") if n.strip()}
    if not wanted:
        raise SystemExit("没有要生成的姿势")
    out: list[tuple[str, dict]] = []
    for key, clip in catalog.items():
        aliases = {key}
        for i, frame in enumerate(clip_frames(clip), start=1):
            fname = (frame.get("file") or f"{key}_{i}.png").strip()
            aliases.add(fname)
            aliases.add(Path(fname).stem)
            aliases.add(f"{key}_{i}")
        if aliases & wanted:
            out.append((key, clip))
    if not out:
        raise SystemExit(f"未知姿势 {names}，按 clip 选：idle, walk, atk …（idle_2 也会整段重出 idle）")
    return out


def open_source(spec: dict) -> Image.Image:
    source_rel = spec.get("source") or ""
    if not source_rel:
        raise SystemExit(f"{spec.get('who')} 没有 source")
    src_path = ART / source_rel
    if not src_path.is_file():
        raise SystemExit(f"找不到大底图: {src_path}")
    return Image.open(src_path).convert("RGBA")


def save_png(im: Image.Image, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGBA").save(dest, format="PNG")


def isolate_on_parchment(piece: Image.Image, pad: int = 28, pad_bottom: int = 56) -> Image.Image:
    rgb = piece.convert("RGB")
    w, h = rgb.size
    canvas = Image.new("RGB", (w + pad * 2, h + pad + pad_bottom), PARCHMENT)
    canvas.paste(rgb, (pad, pad))
    return canvas.convert("RGBA")


def crop_lock(spec: dict) -> tuple[Path, Path]:
    src = open_source(spec)
    tv = spec.get("turnaround")
    if not tv or len(tv) != 4:
        raise SystemExit(f"{spec.get('who')} 没有 turnaround 框")
    lock_rel = spec.get("lock") or f"pose/locks/{spec['who']}.png"
    dest = ART / lock_rel
    save_png(src.crop(tuple(int(x) for x in tv)), dest)
    print("lock", dest.relative_to(REPO).as_posix(), Image.open(dest).size)

    ref_rel = spec.get("lock_ref") or f"pose/locks/{spec['who']}_ref.png"
    ref_dest = ART / ref_rel
    front = spec.get("front")
    if front and len(front) == 4:
        piece = src.crop(tuple(int(x) for x in front))
    else:
        l, t, r, b = [int(x) for x in tv]
        piece = src.crop((l, t + 70, l + (r - l) // 3, b))
    save_png(isolate_on_parchment(piece), ref_dest)
    print("ref", ref_dest.relative_to(REPO).as_posix(), Image.open(ref_dest).size)
    return dest, ref_dest


def resolve_ref(spec: dict, image: str | None) -> Path:
    if image:
        path = Path(image)
        if not path.is_absolute():
            path = (REPO / path).resolve()
        if not path.is_file():
            raise SystemExit(f"找不到锁图: {path}")
        return path
    ref_rel = spec.get("lock_ref") or f"pose/locks/{spec['who']}_ref.png"
    dest = ART / ref_rel
    if dest.is_file():
        return dest
    crop_lock(spec)
    return dest


def as_data_url(path: Path) -> str:
    im = Image.open(path).convert("RGB")
    w, h = im.size
    longest = max(w, h)
    if longest > 1280:
        scale = 1280 / longest
        im = im.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, format="PNG")
    raw = buf.getvalue()
    if len(raw) > 9_500_000:
        buf = io.BytesIO()
        im.save(buf, format="JPEG", quality=90, optimize=True)
        raw = buf.getvalue()
        mime = "image/jpeg"
    else:
        mime = "image/png"
    if len(raw) > 9_500_000:
        raise SystemExit(f"锁图太大 {len(raw)} bytes，先缩小")
    return f"data:{mime};base64," + base64.b64encode(raw).decode("ascii")


def load_style_lock(book: dict) -> str:
    """MiniMax prompt 上限 1500 字，只抽 风格锁.txt 的 ## API_DIGEST。全文给 GPT-image-2。"""
    rel = (book.get("style_lock") or "").strip()
    path = ART / rel if rel else DEFAULT_STYLE
    if not path.is_file():
        return ""
    text = path.read_text(encoding="utf-8")
    marker = "## API_DIGEST"
    if marker in text:
        digest = text.split(marker, 1)[1]
        for stop in ("\n## ", "\n# "):
            cut = digest.find(stop)
            if cut >= 0:
                digest = digest[:cut]
        return " ".join(digest.split())
    return " ".join(text.split())[:700]


def _fit_prompt(parts: list[str], max_chars: int, clip_key: str, kind: str) -> str:
    text = ", ".join(p for p in parts if p)
    if len(text) <= max_chars:
        return text
    head, *rest = parts
    rest_text = ", ".join(p for p in rest if p)
    budget = max_chars - len(rest_text) - 2
    if budget < 80:
        text = text[:max_chars]
    else:
        text = ", ".join(p for p in [head[:budget], rest_text] if p)
    if len(text) > max_chars:
        raise SystemExit(f"{clip_key} {kind} prompt 超过 {max_chars} 字（{len(text)}）")
    return text


def compose_key_prompt(book: dict, spec: dict, clip_key: str, clip: dict, frame: dict, max_chars: int = 4000) -> str:
    note = (frame.get("note") or frame.get("prompt") or "").strip()
    parts = [
        load_style_lock(book),
        (book.get("lock_prompt") or "").strip(),
        (spec.get("who_prompt") or "").strip(),
        "ONE combat sprite, full body with feet, 3/4 facing right, no other people, no turnaround, no labels",
        (clip.get("strip_prompt") or "").strip(),
        f"key pose: {note}" if note else "",
        "character centered on a square, padding on the right for attack effects",
    ]
    return _fit_prompt(parts, max_chars, clip_key, "key")


def compose_tween_prompt(book: dict, spec: dict, clip_key: str, clip: dict, frame: dict, max_chars: int = 4000) -> str:
    note = (frame.get("note") or frame.get("prompt") or "").strip()
    parts = [
        "Keep this exact character, clothes, camera and scale.",
        "in-between animation frame of the SAME shot, not a new illustration",
        (spec.get("who_prompt") or "").strip(),
        f"only change: {note}" if note else "tiny motion only",
        "do not change outfit, do not add backpack, do not move the feet off the ground line",
    ]
    return _fit_prompt(parts, max_chars, clip_key, "tween")


def compose_minimax_prompt(book: dict, spec: dict, clip_key: str, clip: dict, frame: dict) -> str:
    action = (
        (frame.get("prompt") or "").strip()
        or (clip.get("prompt") or "").strip()
        or (clip.get("strip_prompt") or "").strip()
        or (frame.get("note") or "").strip()
    )
    lock = (book.get("minimax_prompt") or "").strip()
    return _fit_prompt([action, lock], 1500, clip_key, "minimax")


def generate_one(
    key: str,
    group: str,
    base: str,
    model: str,
    data_url: str,
    prompt: str,
    aspect: str | None,
    seed: int | None,
    width: int | None = None,
    height: int | None = None,
    n: int = 1,
) -> list[bytes]:
    payload: dict = {
        "model": model,
        "prompt": prompt,
        "n": int(max(1, min(9, n))),
        "prompt_optimizer": False,
        "response_format": "base64",
        "subject_reference": [{"type": "character", "image_file": data_url}],
    }
    if aspect:
        payload["aspect_ratio"] = aspect
    elif width and height:
        payload["width"] = int(width)
        payload["height"] = int(height)
    if seed is not None:
        payload["seed"] = int(seed)
    r = requests.post(
        api_url(base, "/v1/image_generation", group),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
        json=payload,
        timeout=180,
    )
    data = raise_api(r)
    images = (data.get("data") or {}).get("image_base64") or []
    if not images:
        raise SystemExit(f"无 image_base64: {json.dumps(data, ensure_ascii=False)[:500]}")
    out: list[bytes] = []
    for blob in images:
        if "," in blob and blob.strip().startswith("data:"):
            blob = blob.split(",", 1)[1]
        out.append(base64.b64decode(blob))
    return out


def generate_openai(
    key: str,
    base: str,
    model: str,
    quality: str,
    size: str,
    image_paths: list[Path],
    prompt: str,
    background: str,
) -> bytes:
    url = f"{base}/images/edits"
    data = {
        "model": model,
        "prompt": prompt,
        "n": "1",
        "size": size,
        "quality": quality,
        "output_format": "png",
        "background": background,
    }
    handles = [p.open("rb") for p in image_paths]
    try:
        files = [("image", (p.name, h, "image/png")) for p, h in zip(image_paths, handles)]
        r = requests.post(
            url,
            headers={"Authorization": f"Bearer {key}"},
            data=data,
            files=files,
            timeout=180,
        )
    finally:
        for h in handles:
            h.close()
    try:
        payload = r.json()
    except Exception as e:
        raise SystemExit(f"OpenAI 非 JSON {r.status_code}: {r.text[:400]}") from e
    if r.status_code >= 400:
        raise SystemExit(f"OpenAI 错误 {r.status_code}: {json.dumps(payload, ensure_ascii=False)[:800]}")
    images = payload.get("data") or []
    blob = (images[0] or {}).get("b64_json") if images else None
    if not blob:
        raise SystemExit(f"无 b64_json: {json.dumps(payload, ensure_ascii=False)[:500]}")
    return base64.b64decode(blob)


def parchment_matte(im: Image.Image, threshold: int = 42) -> Image.Image:
    rgba = im.convert("RGBA")
    w, h = rgba.size
    raw = list(rgba.getdata())
    data = []
    for px in raw:
        if isinstance(px, tuple) and len(px) >= 4:
            data.append((int(px[0]), int(px[1]), int(px[2]), int(px[3])))
        else:
            continue
    if len(data) != w * h:
        return rgba

    def at(x: int, y: int):
        return data[y * w + x]

    samples = []
    for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3), (w // 2, 2)):
        if not (0 <= x < w and 0 <= y < h):
            continue
        r, g, b, a = at(x, y)
        bright = min(r, g, b) > 180 and max(r, g, b) - min(r, g, b) < 48
        parchment = r > 160 and g > 140 and b > 100 and r >= b
        if a > 200 and (bright or parchment):
            samples.append((r, g, b))
    if not samples:
        return rgba
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


def maybe_knock_bg(im: Image.Image) -> Image.Image:
    rgba = im.convert("RGBA")
    alpha = [px[3] for px in rgba.getdata()]
    if alpha and min(alpha) < 250:
        return rgba
    return parchment_matte(rgba)


def content_bbox(im: Image.Image, a_min: int = 24) -> tuple[int, int, int, int] | None:
    rgba = im.convert("RGBA")
    w, h = rgba.size
    data = list(rgba.getdata())
    xs, ys = [], []
    for i, px in enumerate(data):
        if px[3] >= a_min:
            xs.append(i % w)
            ys.append(i // w)
    if not xs:
        return None
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def column_fill(im: Image.Image, a_min: int = 24) -> list[int]:
    rgba = im.convert("RGBA")
    w, h = rgba.size
    data = list(rgba.getdata())
    col = [0] * w
    for i, px in enumerate(data):
        if px[3] >= a_min:
            col[i % w] += 1
    return col


def split_strip(im: Image.Image, n: int) -> list[Image.Image]:
    if n <= 1:
        return [im]
    w, h = im.size
    col = column_fill(im)
    thresh = max(4, int(h * 0.02))
    blobs: list[tuple[int, int]] = []
    i = 0
    while i < w:
        if col[i] >= thresh:
            j = i
            while j < w and col[j] >= thresh:
                j += 1
            if j - i >= 8:
                blobs.append((i, j))
            i = j
        else:
            i += 1
    blobs.sort(key=lambda b: b[1] - b[0], reverse=True)
    blobs = sorted(blobs[:n], key=lambda b: b[0])
    if len(blobs) != n:
        cell = max(1, w // n)
        return [
            im.crop((i * cell, 0, w if i == n - 1 else (i + 1) * cell, h))
            for i in range(n)
        ]
    pad = 6
    out = []
    for left, right in blobs:
        out.append(im.crop((max(0, left - pad), 0, min(w, right + pad), h)))
    return out


def foot_center_x(im: Image.Image, box: tuple[int, int, int, int]) -> float:
    l, t, r, b = box
    band = max(2, int((b - t) * 0.18))
    feet = im.crop((l, max(t, b - band), r, b))
    fb = content_bbox(feet)
    if fb is None:
        return (l + r) / 2
    return l + (fb[0] + fb[2]) / 2


def normalize_frames(
    cells: list[Image.Image],
    canvas: tuple[int, int],
    ground_frac: float,
    key_index: int | None = None,
) -> list[Image.Image]:
    cw, ch = canvas
    ground = max(4, int(round(ch * ground_frac)))
    measured = []
    for cell in cells:
        knocked = maybe_knock_bg(cell)
        box = content_bbox(knocked)
        measured.append((knocked, box))
    refs: list[tuple[Image.Image, tuple[int, int, int, int] | None]]
    if key_index is not None and 0 <= key_index < len(measured) and measured[key_index][1] is not None:
        refs = [measured[key_index]]
    else:
        refs = measured
    max_h = 1
    max_w = 1
    for knocked, box in refs:
        if box is None:
            continue
        max_w = max(max_w, box[2] - box[0])
        max_h = max(max_h, box[3] - box[1])
    avail_h = max(8, ch - ground - 8)
    avail_w = max(8, int(cw * 0.62))
    scale = min(avail_h / max_h, avail_w / max_w, 1.0)

    out = []
    for knocked, box in measured:
        canvas_im = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        if box is None:
            out.append(canvas_im)
            continue
        l, t, r, b = box
        piece = knocked.crop((l, t, r, b))
        nw = max(1, int(round(piece.size[0] * scale)))
        nh = max(1, int(round(piece.size[1] * scale)))
        piece = piece.resize((nw, nh), Image.Resampling.LANCZOS)
        fx = foot_center_x(knocked, box) - l
        fx = int(round(fx * scale))
        x = int(round(cw * 0.5 - fx))
        y = ch - ground - nh
        canvas_im.paste(piece, (x, y), piece)
        out.append(canvas_im)
    return out


def pack_clip(book: dict, who: str, clip_key: str, clip: dict, sheet: Image.Image, out: Path) -> None:
    frames = clip_frames(clip)
    n = len(frames)
    size = book.get("frame_size") or [512, 512]
    canvas = (int(size[0]), int(size[1]))
    ground = float(book.get("ground_frac") or 0.12)
    cells = split_strip(sheet, n)
    key_i = int(clip.get("key") or 1)
    key_i = min(max(1, key_i), max(1, n))
    packed = normalize_frames(cells, canvas, ground, key_index=key_i - 1)
    meta = {
        "clip": clip_key,
        "canvas": list(canvas),
        "ground_frac": ground,
        "unity_pivot": [0.5, ground],
        "frames": [],
    }
    for i, (frame, cell) in enumerate(zip(frames, packed), start=1):
        fname = (frame.get("file") or f"{clip_key}_{i}.png").strip()
        dest = out / fname
        write_png(cell, dest, "pack")
        box = content_bbox(cell)
        meta["frames"].append({"file": fname, "bbox": list(box) if box else None, "size": list(cell.size)})
    (out / f"_{clip_key}_meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    print("pack", clip_key, canvas, "pivot", (0.5, ground), flush=True)


def strip_size(book: dict, n: int) -> tuple[str | None, int | None, int | None]:
    if n <= 1:
        return (book.get("aspect_ratio") or "3:4").strip(), None, None
    size = book.get("strip_size") or [1536, 704]
    w, h = int(size[0]), int(size[1])
    if n == 2:
        w = 1152
        h = 768
    return None, w, h


def cache_dir(who: str) -> Path:
    d = CACHE / who
    d.mkdir(parents=True, exist_ok=True)
    return d


def write_png(im: Image.Image, dest: Path, src_fmt: str = "?") -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGBA").save(dest, format="PNG")
    print(" ", dest.relative_to(REPO).as_posix(), src_fmt, "→ PNG", im.size, dest.stat().st_size, flush=True)


def cmd_lock(args: argparse.Namespace) -> None:
    book = load_book()
    spec = character(book, args.who)
    if args.image:
        src = Path(args.image)
        if not src.is_absolute():
            src = (REPO / src).resolve()
        if not src.is_file():
            raise SystemExit(f"找不到三视图: {src}")
        dest = ART / (spec.get("lock") or f"pose/locks/{spec['who']}.png")
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dest)
        print("lock copied", dest.relative_to(REPO).as_posix())
        return
    crop_lock(spec)


def pick_candidate(out: Path, clip_key: str, cand: int, dest_index: int = 1) -> Path | None:
    src = out / f"_cand_{clip_key}_{cand}.png"
    if not src.is_file():
        return None
    dest = out / f"_raw_{clip_key}_{dest_index}.png"
    shutil.copy2(src, dest)
    return dest


def cmd_gen(args: argparse.Namespace) -> None:
    book = load_book()
    spec = character(book, args.who)
    clips = pick_clips(book, args.poses)
    lock = resolve_ref(spec, args.image)
    ref = lock.relative_to(REPO).as_posix() if lock.is_relative_to(REPO) else str(lock)
    print("ref", ref, flush=True)
    provider = pose_provider(book)
    seed = spec.get("seed")
    out = cache_dir(spec["who"])
    oa = None
    mm = None
    if provider == "openai":
        oa = openai_cfg(book)
        print("provider openai", oa[2], oa[3], oa[4], flush=True)
    else:
        mm = cfg()
        print("provider minimax", mm[3], flush=True)

    for clip_key, clip in clips:
        frames = clip_frames(clip)
        n = len(frames)
        bg = book.get("background") or "transparent"
        key_i = int(clip.get("key") or 1)
        key_i = min(max(1, key_i), n)
        key_frame = frames[key_i - 1]
        if provider == "openai":
            print("gen", clip_key, "key", key_frame.get("file"), flush=True)
            key, base, model, quality, size = oa
            raw = generate_openai(
                key, base, model, quality, size, [lock],
                compose_key_prompt(book, spec, clip_key, clip, key_frame, 4000), bg,
            )
            key_im = maybe_knock_bg(Image.open(io.BytesIO(raw)).convert("RGBA"))
            key_path = out / f"_key_{clip_key}.png"
            write_png(key_im, key_path, "key")
            write_png(key_im, out / f"_raw_{clip_key}_{key_i}.png", "raw")
            for i, frame in enumerate(frames, start=1):
                if i == key_i:
                    continue
                print("tween", clip_key, frame.get("file"), flush=True)
                raw = generate_openai(
                    key, base, model, quality, size, [key_path],
                    compose_tween_prompt(book, spec, clip_key, clip, frame, 4000), bg,
                )
                tween_im = maybe_knock_bg(Image.open(io.BytesIO(raw)).convert("RGBA"))
                write_png(tween_im, out / f"_raw_{clip_key}_{i}.png", "raw")
            if not pack_raws(book, clip_key, clip, out):
                raise SystemExit(f"{clip_key} 生成后铺帧失败")
            continue

        prompt = compose_minimax_prompt(book, spec, clip_key, clip, key_frame)
        n_cand = int(getattr(args, "candidates", None) or book.get("candidates") or 3)
        n_cand = max(1, min(9, n_cand))
        aspect = (book.get("aspect_ratio") or "1:1").strip()
        print("gen", clip_key, "n", n_cand, "prompt", prompt, flush=True)
        api_key, group, base, model = mm
        use_seed = None if n_cand > 1 else seed
        blobs = generate_one(
            api_key, group, base, model, as_data_url(lock),
            prompt, aspect, use_seed, n=n_cand,
        )
        for i, blob in enumerate(blobs, start=1):
            im = maybe_knock_bg(Image.open(io.BytesIO(blob)).convert("RGBA"))
            write_png(im, out / f"_cand_{clip_key}_{i}.png", "cand")
        pick = int(getattr(args, "pick", None) or 1)
        pick = min(max(1, pick), len(blobs))
        picked = pick_candidate(out, clip_key, pick, dest_index=1)
        if picked is None:
            raise SystemExit(f"{clip_key} 没有候选 {pick}")
        print("pick", clip_key, f"cand {pick}", flush=True)
        if not pack_raws(book, clip_key, clip, out):
            raise SystemExit(f"{clip_key} 生成后铺帧失败")


def pack_raws(book: dict, clip_key: str, clip: dict, out: Path) -> bool:
    frames = clip_frames(clip)
    n = len(frames)
    key_i = int(clip.get("key") or 1)
    key_i = min(max(1, key_i), max(1, n))
    cells: list[Image.Image] = []
    for i in range(1, n + 1):
        raw_path = out / f"_raw_{clip_key}_{i}.png"
        if not raw_path.is_file():
            fallback = out / f"_key_{clip_key}.png" if i == key_i else None
            if fallback is None or not fallback.is_file():
                return False
            raw_path = fallback
        cells.append(maybe_knock_bg(Image.open(raw_path).convert("RGBA")))
    canvas = tuple(int(x) for x in (book.get("frame_size") or [512, 512]))
    packed = normalize_frames(cells, canvas, float(book.get("ground_frac") or 0.12), key_index=key_i - 1)
    meta = {
        "clip": clip_key,
        "canvas": list(canvas),
        "ground_frac": book.get("ground_frac") or 0.12,
        "unity_pivot": [0.5, book.get("ground_frac") or 0.12],
        "key": key_i,
        "frames": [],
    }
    for i, (frame, cell) in enumerate(zip(frames, packed), start=1):
        fname = (frame.get("file") or f"{clip_key}_{i}.png").strip()
        write_png(cell, out / fname, "pack")
        box = content_bbox(cell)
        meta["frames"].append({"file": fname, "bbox": list(box) if box else None, "size": list(cell.size)})
    (out / f"_{clip_key}_meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    print("pack", clip_key, canvas, "key", key_i, flush=True)
    return True


def cmd_pack(args: argparse.Namespace) -> None:
    book = load_book()
    spec = character(book, args.who)
    clips = pick_clips(book, args.poses)
    out = cache_dir(spec["who"])
    cand = int(getattr(args, "cand", 0) or 0)
    for clip_key, clip in clips:
        if cand:
            if pick_candidate(out, clip_key, cand, dest_index=1) is None:
                print("skip 无候选", clip_key, cand)
                continue
            print("pick", clip_key, f"cand {cand}", flush=True)
        if pack_raws(book, clip_key, clip, out):
            continue
        strip = out / f"_strip_{clip_key}.png"
        if not strip.is_file():
            print("skip 无 key/raw 也无横条", clip_key)
            continue
        sheet = maybe_knock_bg(Image.open(strip).convert("RGBA"))
        pack_clip(book, spec["who"], clip_key, clip, sheet, out)


def cmd_import(args: argparse.Namespace) -> None:
    book = load_book()
    spec = character(book, args.who)
    who = spec["who"]
    src = cache_dir(who)
    catalog = pose_catalog(book)
    n = 0
    for clip_key, clip in catalog.items():
        sub = (clip.get("unity_subdir") or "").replace("{who}", who).strip()
        if not sub:
            continue
        for i, frame in enumerate(clip_frames(clip), start=1):
            fname = (frame.get("file") or f"{clip_key}_{i}.png").strip()
            src_file = src / fname
            if not src_file.is_file():
                print("skip 无缓存", fname)
                continue
            dest = REPO / "unity" / "Assets" / "Bundle" / sub / fname
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src_file, dest)
            print("→", dest.relative_to(REPO).as_posix())
            n += 1
    if n == 0:
        raise SystemExit("没有可导入的战斗帧。先 gen")
    print("imported", n)


def main() -> None:
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)

    lock = sub.add_parser("lock", help="从大底裁三视图（给人看）和无字正面（给 API）")
    lock.add_argument("--who", required=True)
    lock.add_argument("--image", default=None, help="现成锁图路径；省略则按 poses.json 裁")
    lock.set_defaults(fn=cmd_lock)

    gen = sub.add_parser("gen", help="MiniMax：锁图+短prompt出候选；OpenAI：key 再补间")
    gen.add_argument("--who", required=True)
    gen.add_argument("--image", default=None, help="这次用的锁图；省略则用 lock_ref")
    gen.add_argument("--poses", default=None, help="clip 名，逗号分隔。例: idle,atk")
    gen.add_argument("--candidates", type=int, default=None, help="MiniMax 一次出几张，默认 poses.json candidates")
    gen.add_argument("--pick", type=int, default=1, help="默认采用第几张候选")
    gen.set_defaults(fn=cmd_gen)

    pack = sub.add_parser("pack", help="已有 raw/候选按脚底对齐到固定画布")
    pack.add_argument("--who", required=True)
    pack.add_argument("--poses", default=None)
    pack.add_argument("--cand", type=int, default=0, help="改用 _cand_{clip}_{n}.png 再铺")
    pack.set_defaults(fn=cmd_pack)

    imp = sub.add_parser("import", help="缓存战斗帧覆盖进 Bundle/Role/{who}/battle")
    imp.add_argument("--who", required=True)
    imp.set_defaults(fn=cmd_import)

    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
