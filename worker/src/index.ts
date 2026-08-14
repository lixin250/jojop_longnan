export interface Env {
  JOJOP_KV: KVNamespace;
  ENVIRONMENT?: string;
}

const DEFAULT_CONFIG = {
  adsEnabled: true,
  rewardedEnabled: true,
  interstitialEnabled: true,
  dailyRewardedCap: 8,
  blockSpeed: 2.4,
  speedRampPerScore: 0.04,
  minOverlapRatio: 0.18,
  interstitialEveryNRetries: 2,
};

const CORS: HeadersInit = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, PUT, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Content-Type": "application/json; charset=utf-8",
};

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: CORS });
    }

    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, "") || "/";

    try {
      if (path === "/config" && request.method === "GET") {
        return await handleGetConfig(env);
      }

      if (path === "/health" && request.method === "GET") {
        return json({ ok: true, env: env.ENVIRONMENT ?? "unknown" });
      }

      const saveMatch = path.match(/^\/save\/([^/]+)$/);
      if (saveMatch) {
        const deviceId = decodeURIComponent(saveMatch[1]);
        if (!isSafeDeviceId(deviceId)) {
          return json({ error: "invalid device id" }, 400);
        }

        if (request.method === "GET") {
          return await handleGetSave(env, deviceId);
        }

        if (request.method === "PUT") {
          return await handlePutSave(env, deviceId, request);
        }
      }

      return json({ error: "not found" }, 404);
    } catch (err) {
      const message = err instanceof Error ? err.message : "unknown error";
      return json({ error: message }, 500);
    }
  },
} satisfies ExportedHandler<Env>;

async function handleGetConfig(env: Env): Promise<Response> {
  const raw = await env.JOJOP_KV.get("config");
  if (!raw) {
    await env.JOJOP_KV.put("config", JSON.stringify(DEFAULT_CONFIG));
    return json(DEFAULT_CONFIG);
  }

  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    return json({ ...DEFAULT_CONFIG, ...parsed });
  } catch {
    return json(DEFAULT_CONFIG);
  }
}

async function handleGetSave(env: Env, deviceId: string): Promise<Response> {
  const raw = await env.JOJOP_KV.get(saveKey(deviceId));
  if (!raw) {
    return json({ error: "not found" }, 404);
  }
  return new Response(raw, { status: 200, headers: CORS });
}

async function handlePutSave(env: Env, deviceId: string, request: Request): Promise<Response> {
  const text = await request.text();
  if (text.length > 8_192) {
    return json({ error: "payload too large" }, 413);
  }

  try {
    JSON.parse(text);
  } catch {
    return json({ error: "invalid json" }, 400);
  }

  await env.JOJOP_KV.put(saveKey(deviceId), text, { expirationTtl: 60 * 60 * 24 * 180 });
  return json({ ok: true });
}

function saveKey(deviceId: string): string {
  return `save:${deviceId}`;
}

function isSafeDeviceId(id: string): boolean {
  return id.length > 0 && id.length <= 128 && /^[A-Za-z0-9_\-:.=]+$/.test(id);
}

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), { status, headers: CORS });
}
