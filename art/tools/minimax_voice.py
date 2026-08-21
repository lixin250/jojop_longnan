#!/usr/bin/env python3
"""一段 {who}.mp3 锁音色 → 按 RoleVoice 表批量出普通话 ogg。

  python art/tools/minimax_voice.py clone
  python art/tools/minimax_voice.py synth
  python art/tools/minimax_voice.py import
  python art/tools/minimax_voice.py sync    # lines.csv → 语音.xlsx，给 Luban
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import shutil
import subprocess
from pathlib import Path

import requests

REPO = Path(__file__).resolve().parents[2]
VOICE_DIR = REPO / "art" / "voice"
CACHE = VOICE_DIR / ".cache"
BUNDLE_ROLE = REPO / "unity" / "Assets" / "Bundle" / "Role"
LINES = VOICE_DIR / "lines.csv"
VOICES_JSON = VOICE_DIR / "voices.json"
SAMPLE_EXTS = (".mp3", ".wav", ".m4a")


def load_env() -> dict[str, str]:
    env = dict(os.environ)
    path = VOICE_DIR / "secrets.env"
    if path.is_file():
        for raw in path.read_text(encoding="utf-8").splitlines():
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
    model = env.get("MINIMAX_MODEL", "speech-2.8-hd").strip() or "speech-2.8-hd"
    return key, group, base, model


def url(base: str, path: str, group: str) -> str:
    u = f"{base}{path}"
    if group:
        sep = "&" if "?" in u else "?"
        u = f"{u}{sep}GroupId={group}"
    return u


def headers(key: str) -> dict[str, str]:
    return {"Authorization": f"Bearer {key}"}


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


def load_lines() -> list[dict[str, str]]:
    with LINES.open(encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    out = []
    for row in rows:
        rec = {k: (v or "").strip() for k, v in row.items() if k}
        if not rec.get("id"):
            continue
        who = rec.get("who") or rec["id"].split("_", 1)[0]
        rec["who"] = who
        rec["module"] = rec.get("module") or "battle"
        rec["meaning"] = rec.get("meaning") or "line"
        expect = f"{rec['who']}_{rec['module']}_{rec['meaning']}"
        if rec["id"] != expect:
            raise SystemExit(f"id 必须是 who_module_meaning：{rec['id']} ≠ {expect}")
        rec["langPath"] = rec.get("langPath") or f"{who}/voice/{rec['id']}"
        rec["langPath_ln"] = rec.get("langPath_ln") or ""
        rec["text_zh"] = rec.get("text_zh") or ""
        rec["emotion"] = rec.get("emotion") or "neutral"
        out.append(rec)
    return out


def load_voice_specs() -> dict[str, dict]:
    specs: dict[str, dict] = {}
    shared_prompt = ""
    if VOICES_JSON.is_file():
        data = json.loads(VOICES_JSON.read_text(encoding="utf-8"))
        shared_prompt = (data.get("prompt_text") or "").strip()
        for spec in data.get("voices") or []:
            who = (spec.get("who") or "").strip()
            if who:
                specs[who] = spec
    for who in {r["who"] for r in load_lines()}:
        specs.setdefault(who, {"who": who})
        spec = specs[who]
        spec["voice_id"] = spec.get("voice_id") or f"JojoP_{who}"
        spec["prompt_text"] = spec.get("prompt_text") or shared_prompt
        spec.setdefault("prompt", f"samples/{who}_prompt.mp3")
        sample = spec.get("sample")
        if sample:
            spec["sample"] = sample
        else:
            hit = next((VOICE_DIR / "samples" / f"{who}{ext}" for ext in SAMPLE_EXTS if (VOICE_DIR / "samples" / f"{who}{ext}").is_file()), None)
            spec["sample"] = str((hit or (VOICE_DIR / "samples" / f"{who}.mp3")).relative_to(VOICE_DIR).as_posix())
    return specs


def sample_path(spec: dict) -> Path:
    return VOICE_DIR / spec["sample"]


def upload(key: str, group: str, base: str, path: Path, purpose: str) -> int:
    if not path.is_file():
        raise SystemExit(f"找不到录音: {path}  （每人一段 samples/{{who}}.mp3）")
    with path.open("rb") as f:
        files = {"file": (path.name, f)}
        r = requests.post(
            url(base, "/v1/files/upload", group),
            headers=headers(key),
            data={"purpose": purpose},
            files=files,
            timeout=120,
        )
    data = raise_api(r)
    file_id = (data.get("file") or {}).get("file_id")
    if file_id is None:
        raise SystemExit(f"上传无 file_id: {data}")
    print("  uploaded", path.name, "→", file_id)
    return int(file_id)


def clone_one(key: str, group: str, base: str, model: str, spec: dict) -> None:
    file_id = upload(key, group, base, sample_path(spec), "voice_clone")
    preview = spec.get("prompt_text") or "这是克隆预览。"
    payload: dict = {
        "file_id": file_id,
        "voice_id": spec["voice_id"],
        "model": model,
        "need_noise_reduction": True,
        "need_volume_normalization": True,
        "text": preview,
        "language_boost": spec.get("language_boost") or "Chinese",
    }
    prompt_rel = spec.get("prompt")
    if prompt_rel:
        prompt_path = VOICE_DIR / prompt_rel
        if prompt_path.is_file():
            prompt_id = upload(key, group, base, prompt_path, "prompt_audio")
            payload["clone_prompt"] = {
                "prompt_audio": prompt_id,
                "prompt_text": spec.get("prompt_text") or "",
            }
    r = requests.post(
        url(base, "/v1/voice_clone", group),
        headers={**headers(key), "Content-Type": "application/json"},
        json=payload,
        timeout=180,
    )
    data = raise_api(r)
    CACHE.mkdir(parents=True, exist_ok=True)
    state_path = CACHE / "cloned.json"
    state = json.loads(state_path.read_text(encoding="utf-8")) if state_path.is_file() else {}
    state[spec["who"]] = {
        "voice_id": spec["voice_id"],
        "file_id": file_id,
        "raw": {k: data.get(k) for k in ("input_sensitive", "demo_audio") if k in data},
    }
    state_path.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
    print("cloned", spec["who"], spec["voice_id"])


def t2a(key: str, group: str, base: str, model: str, voice_id: str, text: str, emotion: str) -> bytes:
    payload = {
        "model": model,
        "text": text,
        "stream": False,
        "language_boost": "Chinese",
        "voice_setting": {
            "voice_id": voice_id,
            "speed": 1,
            "vol": 1,
            "pitch": 0,
            "emotion": emotion or "neutral",
        },
        "audio_setting": {
            "sample_rate": 32000,
            "bitrate": 128000,
            "format": "mp3",
            "channel": 1,
        },
    }
    r = requests.post(
        url(base, "/v1/t2a_v2", group),
        headers={**headers(key), "Content-Type": "application/json"},
        json=payload,
        timeout=180,
    )
    data = raise_api(r)
    hex_audio = (data.get("data") or {}).get("audio")
    if not hex_audio:
        raise SystemExit(f"无 audio 字段: {json.dumps(data, ensure_ascii=False)[:500]}")
    return bytes.fromhex(hex_audio)


def to_ogg(src: Path, dst: Path) -> Path:
    dst.parent.mkdir(parents=True, exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        subprocess.run(
            [ffmpeg, "-y", "-i", str(src), "-c:a", "libvorbis", "-q:a", "6", str(dst)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        return dst
    alt = dst.with_suffix(".mp3")
    shutil.copy2(src, alt)
    print("  无 ffmpeg，留下", alt.name)
    return alt


def bundle_file(lang_path: str, ext: str) -> Path:
    return BUNDLE_ROLE / f"{lang_path}{ext}"


def cmd_clone(_: argparse.Namespace) -> None:
    key, group, base, model = cfg()
    specs = load_voice_specs()
    n = 0
    for who, spec in specs.items():
        path = sample_path(spec)
        if not path.is_file():
            print("skip 无样本", who, path.as_posix())
            continue
        print("clone", who)
        clone_one(key, group, base, model, spec)
        n += 1
    if n == 0:
        raise SystemExit("没有可克隆的样本。把 10～60 秒人声放到 art/voice/samples/lixin.mp3")


def cmd_synth(_: argparse.Namespace) -> None:
    key, group, base, model = cfg()
    specs = load_voice_specs()
    out_dir = CACHE / "synth"
    out_dir.mkdir(parents=True, exist_ok=True)
    for row in load_lines():
        text = row["text_zh"]
        if not text:
            print("skip 空 text_zh", row["id"])
            continue
        spec = specs.get(row["who"])
        if spec is None:
            print("skip 无音色", row["id"])
            continue
        stem = row["id"]
        mp3 = out_dir / f"{stem}.mp3"
        print("synth", stem, text[:24])
        raw = t2a(key, group, base, model, spec["voice_id"], text, row["emotion"])
        mp3.write_bytes(raw)
        to_ogg(mp3, out_dir / f"{stem}.ogg")
        meta = out_dir / f"{stem}.json"
        meta.write_text(json.dumps({"id": row["id"], "langPath": row["langPath"]}, ensure_ascii=False), encoding="utf-8")


def cmd_import(_: argparse.Namespace) -> None:
    src = CACHE / "synth"
    if not src.is_dir():
        raise SystemExit("先跑 synth")
    by_id = {r["id"]: r for r in load_lines()}
    n = 0
    for p in sorted(src.glob("*.ogg")) + sorted(src.glob("*.mp3")):
        if p.suffix == ".mp3" and p.with_suffix(".ogg").exists():
            continue
        row = by_id.get(p.stem)
        if row is None:
            print("skip 不在表里", p.name)
            continue
        dest = bundle_file(row["langPath"], p.suffix)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, dest)
        print("→", dest.relative_to(REPO))
        n += 1
    print("imported", n, "（不会覆盖 langPath_ln）")


def cmd_sync(_: argparse.Namespace) -> None:
    script = REPO / "art" / "tools" / "sync_role_voice.mjs"
    node = shutil.which("node")
    if not node:
        raise SystemExit("需要 node 才能把 lines.csv 写成 Luban 的 语音.xlsx")
    subprocess.run([node, str(script)], check=True, cwd=str(REPO))


SYSTEM_VOICES = ("male-qn-qingse", "female-shaonv", "presenter_male")


def cmd_ping(_: argparse.Namespace) -> None:
    key, group, base, model = cfg()
    print("key_len", len(key), "group", "yes" if group else "NO", "base", base, "model", model)
    CACHE.mkdir(parents=True, exist_ok=True)
    last_err = None
    for voice in SYSTEM_VOICES:
        try:
            print("try voice", voice)
            raw = t2a(key, group, base, model, voice, "来了来了，开机干活。", "neutral")
            out = CACHE / "ping.mp3"
            out.write_bytes(raw)
            to_ogg(out, CACHE / "ping.ogg")
            print("ok", out, "bytes", len(raw))
            return
        except SystemExit as e:
            last_err = e
            print("fail", voice, e)
    raise SystemExit(last_err or "ping 失败")


def main() -> None:
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("ping").set_defaults(fn=cmd_ping)
    sub.add_parser("clone").set_defaults(fn=cmd_clone)
    sub.add_parser("synth").set_defaults(fn=cmd_synth)
    sub.add_parser("import").set_defaults(fn=cmd_import)
    sub.add_parser("sync").set_defaults(fn=cmd_sync)
    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
