using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class HttpApiClient
{
    private static string _cookieHeader;
    private static string _xsrfToken;
    private static string _accessToken;
    private static string _refreshToken;

    /// <summary>
    /// Perform GET request with cookies and XSRF token if available.
    /// </summary>
    public static async Task<string> GetAsync(string url)
    {
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "GET " + url);
        return await SendRequestAsync(url, "GET");
    }

    /// <summary>
    /// Perform POST request with JSON body, replay-protection headers, and optional game secret.
    /// Automatically refreshes token on 401/403.
    /// </summary>
    public static async Task<string> PostAsync(string url, string jsonBody = null, bool requireGameSecret = false)
    {
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "POST " + url);
        return await SendRequestAsync(url, "POST", jsonBody, isRetry: false, authToken: null, requireGameSecret: requireGameSecret);
    }

    /// <summary>PUT with replay-protection headers (backend requires x-nonce / x-timestamp on mutating requests).</summary>
    public static async Task<string> PutAsync(string url, string jsonBody = null, bool requireGameSecret = false)
    {
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "PUT " + url);
        return await SendRequestAsync(url, "PUT", jsonBody, isRetry: false, authToken: null, requireGameSecret: requireGameSecret);
    }

    private static async Task<string> SendRequestAsync(
        string url,
        string method,
        string jsonBody = null,
        bool isRetry = false,
        string authToken = null,
        bool requireGameSecret = false)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, method))
        {
            bool hasBody = (method == "POST" || method == "PUT" || method == "PATCH") && !string.IsNullOrEmpty(jsonBody);
            if (hasBody)
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.SetRequestHeader("Content-Type", "application/json");
            }
            else if (method == "POST" || method == "PUT" || method == "PATCH")
            {
                // Empty JSON body so Content-Type is still set for APIs that expect JSON.
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{}");
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.downloadHandler = new DownloadHandlerBuffer();

            AttachAuthHeaders(req, authToken);

            if (method == "POST" || method == "PUT" || method == "PATCH")
                AttachReplayProtectionHeaders(req);

            if (requireGameSecret)
                AttachGameSecretHeader(req);

            var tcs = new TaskCompletionSource<UnityWebRequest>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(req);
            await tcs.Task;

            SaveCookies(req);

            string responsePreview = req.downloadHandler != null ? req.downloadHandler.text : null;
            bool isReplayReject = IsReplayProtectionReject(req.responseCode, responsePreview);

            // Only refresh session on real auth failures — not on replay/timestamp rejects (403 Request Expired).
            if ((req.responseCode == 401 || (req.responseCode == 403 && !isReplayReject)) && !isRetry)
            {
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                    "Request failed with " + req.responseCode + ". Trying refresh...");
                bool refreshed = await RefreshSessionAsync();
                if (refreshed)
                {
                    TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Session refreshed. Retrying original request...");
                    return await SendRequestAsync(url, method, jsonBody, true, authToken, requireGameSecret);
                }
            }

            string cleanMessage = responsePreview;

            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                string rawText = responsePreview;
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                    method + " " + url + " failed (" + req.responseCode + "): " + rawText);

                cleanMessage = ExtractErrorMessage(rawText);
                throw new System.Exception(cleanMessage);
            }

            return cleanMessage;
        }
    }

    static bool IsReplayProtectionReject(long statusCode, string body)
    {
        if (statusCode != 400 && statusCode != 403) return false;
        if (string.IsNullOrEmpty(body)) return false;
        return body.IndexOf("Request Expired", StringComparison.OrdinalIgnoreCase) >= 0
               || body.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0
               || body.IndexOf("nonce", StringComparison.OrdinalIgnoreCase) >= 0
               || body.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0
               || body.IndexOf("replay", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Backend replay protection: unique nonce + Unix timestamp in <b>milliseconds</b> (Node Date.now()).
    /// Seconds are rejected as "Request Expired" because the server compares against ms clocks.
    /// </summary>
    static void AttachReplayProtectionHeaders(UnityWebRequest req)
    {
        string nonce = Guid.NewGuid().ToString("N");
        // Milliseconds — matches typical Node replay middleware (Date.now()).
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        req.SetRequestHeader("x-nonce", nonce);
        req.SetRequestHeader("x-timestamp", timestamp);
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "replay headers nonce=" + nonce.Substring(0, 8) + "… tsMs=" + timestamp);
    }

    /// <summary>
    /// Required for match result / walkover. Value from TrucoClientSettings (Resources)
    /// or PlayerPrefs key <c>TrucoGameSecret</c> (runtime override without rebuild).
    /// </summary>
    static void AttachGameSecretHeader(UnityWebRequest req)
    {
        string secret = ResolveGameSecret();
        if (string.IsNullOrEmpty(secret))
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                "x-game-secret MISSING — set Resources/TrucoClientSettings.gameSecret (or PlayerPrefs TrucoGameSecret). POST /result will 403.");
            throw new System.Exception(
                "x-game-secret missing on client — ask backend for the production secret and set TrucoClientSettings.gameSecret");
        }
        req.SetRequestHeader("x-game-secret", secret);
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api,
            "x-game-secret attached (len=" + secret.Length + ")");
    }

    /// <summary>Settings asset first, then PlayerPrefs override for devices already built.</summary>
    public static string ResolveGameSecret()
    {
        string fromSettings = TrucoClientSettings.GameSecret;
        if (!string.IsNullOrEmpty(fromSettings)) return fromSettings.Trim();
        string fromPrefs = PlayerPrefs.GetString("TrucoGameSecret", string.Empty);
        return string.IsNullOrEmpty(fromPrefs) ? string.Empty : fromPrefs.Trim();
    }

    public static bool HasGameSecretConfigured() => !string.IsNullOrEmpty(ResolveGameSecret());

    private static string ExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return "Unknown error";

        string trimmed = responseText.TrimStart();
        if (trimmed.StartsWith("{"))
        {
            try
            {
                var json = JsonUtility.FromJson<ErrorResponse>(responseText);
                if (json != null)
                {
                    if (!string.IsNullOrEmpty(json.error))
                        return json.error.Trim();
                    if (!string.IsNullOrEmpty(json.message))
                        return json.message.Trim();
                }
            }
            catch { }
        }

        return responseText;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string error;
        public string message;
    }

    private static void AttachAuthHeaders(UnityWebRequest req, string explicitToken = null)
    {
        if (!string.IsNullOrEmpty(_cookieHeader))
            req.SetRequestHeader("Cookie", _cookieHeader);

        if (!string.IsNullOrEmpty(_xsrfToken))
            req.SetRequestHeader("X-XSRF-TOKEN", _xsrfToken);

        string tokenToUse = !string.IsNullOrEmpty(explicitToken) ? explicitToken : _accessToken;

        if (!string.IsNullOrEmpty(tokenToUse))
        {
            req.SetRequestHeader("Authorization", "Bearer " + tokenToUse);
            req.SetRequestHeader("x-auth-token", tokenToUse);
        }
    }

    public static void SetAuthToken(string token)
    {
        _accessToken = token;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Auth token manually set.");
    }

    /// <summary>Clears bearer/cookie session so further requests are unauthenticated (e.g. after logout).</summary>
    public static void ClearAuthSession()
    {
        _accessToken = null;
        _refreshToken = null;
        _xsrfToken = null;
        _cookieHeader = null;
    }

    private static void SaveCookies(UnityWebRequest req)
    {
        string setCookie = req.GetResponseHeader("Set-Cookie");
        if (string.IsNullOrEmpty(setCookie)) return;

        List<string> cookies = new List<string>();

        string[] rawParts = setCookie.Split(',');
        string currentPart = "";

        foreach (var rawPart in rawParts)
        {
            string part = rawPart.Trim();
            if (string.IsNullOrEmpty(part)) continue;

            if (currentPart == "")
            {
                currentPart = part;
            }
            else
            {
                if (!part.Contains("=") ||
                    currentPart.ToLower().EndsWith("expires=mon") || currentPart.ToLower().EndsWith("expires=tue") ||
                    currentPart.ToLower().EndsWith("expires=wed") || currentPart.ToLower().EndsWith("expires=thu") ||
                    currentPart.ToLower().EndsWith("expires=fri") || currentPart.ToLower().EndsWith("expires=sat") ||
                    currentPart.ToLower().EndsWith("expires=sun"))
                {
                    currentPart += ", " + part;
                }
                else
                {
                    ProcessCookieString(currentPart, cookies);
                    currentPart = part;
                }
            }
        }
        if (!string.IsNullOrEmpty(currentPart))
            ProcessCookieString(currentPart, cookies);

        _cookieHeader = string.Join("; ", cookies);

        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Saved cookies: " + _cookieHeader);
        if (!string.IsNullOrEmpty(_xsrfToken))
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Saved XSRF token: " + _xsrfToken);
    }

    private static void ProcessCookieString(string rawCookie, List<string> cookies)
    {
        string[] pairs = rawCookie.Split(';');
        string firstPair = pairs[0].Trim();
        if (string.IsNullOrEmpty(firstPair) || !firstPair.Contains("=")) return;

        cookies.Add(firstPair);

        string[] kv = firstPair.Split(new[] { '=' }, 2);
        string key = kv[0].Trim();
        string val = kv[1].Trim();

        if (key.Equals("access_token", System.StringComparison.OrdinalIgnoreCase))
            _accessToken = val;
        else if (key.Equals("refresh_token", System.StringComparison.OrdinalIgnoreCase))
            _refreshToken = val;
        else if (key.Equals("XSRF-TOKEN", System.StringComparison.OrdinalIgnoreCase))
            _xsrfToken = val;
    }

    private static async Task<bool> RefreshSessionAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api, "No refresh token found. Cannot refresh session.");
            return false;
        }

        TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Refreshing session...");

        using (UnityWebRequest req = UnityWebRequest.PostWwwForm(ApiConfig.RefreshToken, ""))
        {
            AttachAuthHeaders(req);
            AttachReplayProtectionHeaders(req);

            var tcs = new TaskCompletionSource<UnityWebRequest>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(req);
            await tcs.Task;

            if (req.result != UnityWebRequest.Result.Success)
            {
                TrucoDebugLog.Error(TrucoDebugLog.Category.Api, "Refresh failed: " + req.error);
                return false;
            }

            SaveCookies(req);
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Refresh successful.");
            return true;
        }
    }
}
