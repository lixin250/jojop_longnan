using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace JojoP.Backend
{
    /// <summary>
    /// Cloudflare 轻后端客户端：远程配置 + 轻量存档。
    /// Thin CF client for /config and /save.
    /// </summary>
    public sealed class CloudflareApiClient
    {
        readonly BackendConfig _config;

        public CloudflareApiClient(BackendConfig config) => _config = config;

        public bool HasBaseUrl =>
            _config != null && !string.IsNullOrWhiteSpace(_config.baseUrl);

        public async Task<RemoteGameConfig> FetchConfigAsync()
        {
            if (!HasBaseUrl) return new RemoteGameConfig();

            string url = $"{_config.baseUrl.TrimEnd('/')}/config";
            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.CeilToInt(_config.requestTimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JojoP.Backend] GET /config 失败: {req.error}");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<RemoteGameConfig>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JojoP.Backend] 配置解析失败: {e.Message}");
                return null;
            }
        }

        public async Task<string> GetSaveAsync(string deviceId)
        {
            if (!HasBaseUrl || string.IsNullOrEmpty(deviceId)) return null;

            string url = $"{_config.baseUrl.TrimEnd('/')}/save/{UnityWebRequest.EscapeURL(deviceId)}";
            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.CeilToInt(_config.requestTimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.responseCode == 404) return null;
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JojoP.Backend] GET /save 失败: {req.error}");
                return null;
            }

            return req.downloadHandler.text;
        }

        public async Task<bool> PutSaveAsync(string deviceId, string jsonBody)
        {
            if (!HasBaseUrl || string.IsNullOrEmpty(deviceId)) return false;

            string url = $"{_config.baseUrl.TrimEnd('/')}/save/{UnityWebRequest.EscapeURL(deviceId)}";
            byte[] body = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(_config.requestTimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JojoP.Backend] PUT /save 失败: {req.error}");
                return false;
            }

            return true;
        }
    }
}
