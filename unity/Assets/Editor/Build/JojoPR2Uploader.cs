#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace JojoP.EditorTools.Build
{
    /// <summary>
    /// 编辑器直传 Cloudflare R2（S3 API）。客户端下载走 r2.dev 公开 URL，不经过 Worker。
    /// </summary>
    public static class JojoPR2Uploader
    {
        const string Region = "auto";
        const string Service = "s3";
        const string Unsigned = "UNSIGNED-PAYLOAD";

        public sealed class Credentials
        {
            public string AccountId;
            public string AccessKeyId;
            public string SecretAccessKey;
            public string Bucket;
        }

        public sealed class Result
        {
            public int Uploaded;
            public int Skipped;
            public int Failed;
            public readonly List<string> Errors = new List<string>();
        }

        static JojoPR2Uploader()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
        }

        public static Result UploadFolder(
            string localDir,
            Credentials creds,
            string objectPrefix,
            bool forceOverwrite,
            Action<int, int, string> onProgress)
        {
            Validate(creds);
            if (string.IsNullOrEmpty(localDir) || !Directory.Exists(localDir))
                throw new DirectoryNotFoundException(localDir);

            string prefix = (objectPrefix ?? string.Empty).Trim().Trim('/');
            var files = Directory.GetFiles(localDir, "*", SearchOption.AllDirectories);
            int total = files.Length;
            var result = new Result();
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string rel = file.Substring(localDir.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                string key = string.IsNullOrEmpty(prefix) ? rel : prefix + "/" + rel;
                onProgress?.Invoke(i, total, rel);

                bool always = forceOverwrite || ShouldAlwaysUpload(rel);
                if (!always && rel.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                {
                    if (HeadExists(creds, key))
                    {
                        result.Skipped++;
                        continue;
                    }
                }

                try
                {
                    PutFile(creds, key, file);
                    result.Uploaded++;
                }
                catch (Exception e)
                {
                    result.Failed++;
                    result.Errors.Add($"{rel}: {e.Message}");
                    Debug.LogError($"[JojoP R2] PUT {key} 失败: {e.Message}");
                }
            }

            onProgress?.Invoke(total, total, "完成");
            return result;
        }

        public static string GetText(string url, int timeoutMs = 20000)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutMs;
            using var resp = (HttpWebResponse)req.GetResponse();
            int code = (int)resp.StatusCode;
            if (code < 200 || code >= 300)
                throw new Exception($"HTTP {code}");
            using var stream = resp.GetResponseStream();
            if (stream == null) return string.Empty;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd().Trim();
        }

        public static void DownloadToFile(string url, string destPath, int timeoutMs = 180000)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            using var resp = (HttpWebResponse)req.GetResponse();
            int code = (int)resp.StatusCode;
            if (code < 200 || code >= 300)
                throw new Exception($"HTTP {code}");
            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? ".");
            using var stream = resp.GetResponseStream();
            using var fs = File.Create(destPath);
            stream?.CopyTo(fs);
        }

        static void Validate(Credentials creds)
        {
            if (creds == null
                || string.IsNullOrEmpty(creds.AccountId)
                || string.IsNullOrEmpty(creds.AccessKeyId)
                || string.IsNullOrEmpty(creds.SecretAccessKey)
                || string.IsNullOrEmpty(creds.Bucket))
            {
                throw new InvalidOperationException(
                    "缺少 R2 凭证。Dashboard → R2 → Manage R2 API Tokens 建 Access Key，填到构建窗口。");
            }
        }

        static bool ShouldAlwaysUpload(string relativePath)
        {
            string lower = relativePath.ToLowerInvariant();
            return lower.EndsWith(".version")
                   || lower.EndsWith(".hash")
                   || lower.EndsWith(".json")
                   || lower.Contains("manifest");
        }

        static bool HeadExists(Credentials creds, string key)
        {
            try
            {
                var req = SignedRequest(creds, "HEAD", key, 20000, 0);
                using var resp = (HttpWebResponse)req.GetResponse();
                return (int)resp.StatusCode == 200;
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse http && (int)http.StatusCode == 404)
                    return false;
                Debug.LogWarning($"[JojoP R2] HEAD 失败，改为直接 PUT: {ex.Message}");
                return false;
            }
        }

        static void PutFile(Credentials creds, string key, string filePath)
        {
            var info = new FileInfo(filePath);
            var req = SignedRequest(creds, "PUT", key, 180000, info.Length);
            req.AllowWriteStreamBuffering = false;
            using (var fs = info.OpenRead())
            using (var stream = req.GetRequestStream())
                fs.CopyTo(stream);

            using var resp = (HttpWebResponse)req.GetResponse();
            int code = (int)resp.StatusCode;
            if (code < 200 || code >= 300)
                throw new Exception($"HTTP {code}");
        }

        static HttpWebRequest SignedRequest(
            Credentials creds, string method, string key, int timeoutMs, long contentLength)
        {
            string host = $"{creds.AccountId}.r2.cloudflarestorage.com";
            string encodedKey = EncodeKey(key);
            string canonicalUri = $"/{creds.Bucket}/{encodedKey}";
            var now = DateTime.UtcNow;
            string amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            string dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            string url = "https://" + host + canonicalUri;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Host = host;
            if (contentLength >= 0 && method == "PUT")
                req.ContentLength = contentLength;

            req.Headers["x-amz-date"] = amzDate;
            req.Headers["x-amz-content-sha256"] = Unsigned;
            if (method == "PUT")
                req.ContentType = "application/octet-stream";

            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = host,
                ["x-amz-content-sha256"] = Unsigned,
                ["x-amz-date"] = amzDate
            };
            if (method == "PUT")
                headers["content-type"] = "application/octet-stream";

            string signedHeaders = string.Join(";", headers.Keys);
            var canonicalHeaders = new StringBuilder();
            foreach (var kv in headers)
                canonicalHeaders.Append(kv.Key).Append(':').Append(kv.Value).Append('\n');

            string canonicalRequest = string.Join("\n",
                method,
                canonicalUri,
                "",
                canonicalHeaders.ToString().TrimEnd('\n') + "\n",
                signedHeaders,
                Unsigned);

            string credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
            string stringToSign = string.Join("\n",
                "AWS4-HMAC-SHA256",
                amzDate,
                credentialScope,
                Hex(Sha256(canonicalRequest)));

            byte[] signingKey = DeriveSigningKey(creds.SecretAccessKey, dateStamp);
            string signature = Hex(Hmac(signingKey, stringToSign));
            req.Headers["Authorization"] =
                $"AWS4-HMAC-SHA256 Credential={creds.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
            return req;
        }

        static string EncodeKey(string key)
        {
            var parts = (key ?? "").Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Uri.EscapeDataString(parts[i]);
            return string.Join("/", parts);
        }

        static byte[] DeriveSigningKey(string secret, string dateStamp)
        {
            byte[] kDate = Hmac(Encoding.UTF8.GetBytes("AWS4" + secret), dateStamp);
            byte[] kRegion = Hmac(kDate, Region);
            byte[] kService = Hmac(kRegion, Service);
            return Hmac(kService, "aws4_request");
        }

        static byte[] Sha256(string text)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        static byte[] Hmac(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif
