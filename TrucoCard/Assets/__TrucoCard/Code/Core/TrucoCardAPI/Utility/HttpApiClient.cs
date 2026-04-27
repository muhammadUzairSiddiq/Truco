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
        Debug.Log("[HttpApiClient] GET " + url);
        return await SendRequestAsync(url, "GET");
    }

    /// <summary>
    /// Perform POST request with JSON body and XSRF token if available.
    /// Automatically refreshes token on 401/403.
    /// </summary>
    public static async Task<string> PostAsync(string url, string jsonBody = null)
    {
        Debug.Log("[HttpApiClient] POST " + url);
        return await SendRequestAsync(url, "POST", jsonBody);
    }

    private static async Task<string> SendRequestAsync(string url, string method, string jsonBody = null, bool isRetry = false, string authToken = null)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, method))
        {
            if (method == "POST" && !string.IsNullOrEmpty(jsonBody))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            
            // If an explicit token is provided for this request, use it.
            // Otherwise, use the globally stored tokens.
            AttachAuthHeaders(req, authToken);

            var tcs = new TaskCompletionSource<UnityWebRequest>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(req);
            await tcs.Task;

            SaveCookies(req);
            
            // 🔹 Handle unauthorized/forbidden with session refresh (unless it is already a retry)
            if ((req.responseCode == 401 || req.responseCode == 403) && !isRetry)
            {
                Debug.LogWarning($"[HttpApiClient] Request failed with {req.responseCode}. Trying refresh...");
                bool refreshed = await RefreshSessionAsync();
                if (refreshed)
                {
                    Debug.Log("[HttpApiClient] Session refreshed. Retrying original request...");
                    return await SendRequestAsync(url, method, jsonBody, true);
                }
            }

            string cleanMessage = req.downloadHandler.text;

            // 🔹 If server returned an error (e.g., 400, 401, 500)
            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                string rawText = req.downloadHandler.text;
                Debug.Log($"{method} {url} failed ({req.responseCode}): {rawText}");

                // 🔹 Try to extract "error" field from JSON
                cleanMessage = ExtractErrorMessage(rawText);

                throw new System.Exception(cleanMessage);
            }

            return cleanMessage;
        }
    }

    private static string ExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
            return "Unknown error";

        try
        {
            // Try to parse JSON: {"error":"Invalid credentials"}
            var json = JsonUtility.FromJson<ErrorResponse>(responseText);
            if (json != null && !string.IsNullOrEmpty(json.error))
                return json.error;
        }
        catch { }

        // Fallback: return raw text
        return responseText;
    }

    [System.Serializable]
    private class ErrorResponse
    {
        public string error;
    }

    private static void AttachAuthHeaders(UnityWebRequest req, string explicitToken = null)
    {
        if (!string.IsNullOrEmpty(_cookieHeader))
            req.SetRequestHeader("Cookie", _cookieHeader);

        if (!string.IsNullOrEmpty(_xsrfToken))
            req.SetRequestHeader("X-XSRF-TOKEN", _xsrfToken);
            
        // Prioritize explicit token, then fall back to saved access token
        string tokenToUse = !string.IsNullOrEmpty(explicitToken) ? explicitToken : _accessToken;
        
        if (!string.IsNullOrEmpty(tokenToUse))
        {
            req.SetRequestHeader("Authorization", "Bearer " + tokenToUse);
            // Some APIs also look for the token in custom headers
            req.SetRequestHeader("x-auth-token", tokenToUse);
        }
    }

    public static void SetAuthToken(string token)
    {
        _accessToken = token;
        Debug.Log("[HttpApiClient] Auth token manually set.");
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
        
        // Use a more robust split that doesn't break on commas in dates
        // Unity merges multiple Set-Cookie headers with ", "
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
                // If the part doesn't contain an '=' or looks like a date continuation, append it
                // Cookie attributes like expires=Thu, 01 Jan 2026 ... get split at the comma
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
                    // New cookie starts
                    ProcessCookieString(currentPart, cookies);
                    currentPart = part;
                }
            }
        }
        if (!string.IsNullOrEmpty(currentPart))
            ProcessCookieString(currentPart, cookies);

        _cookieHeader = string.Join("; ", cookies);

        Debug.Log($"[HttpApiClient] Saved cookies: {_cookieHeader}");
        if (!string.IsNullOrEmpty(_xsrfToken))
            Debug.Log($"[HttpApiClient] Saved XSRF token: {_xsrfToken}");
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

    /// <summary>
    /// Refresh session tokens using refresh_token cookie.
    /// </summary>
    private static async Task<bool> RefreshSessionAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            Debug.LogWarning("[HttpApiClient] No refresh token found. Cannot refresh session.");
            return false;
        }

        Debug.Log("[HttpApiClient] Refreshing session...");

        using (UnityWebRequest req = UnityWebRequest.PostWwwForm(ApiConfig.RefreshToken, ""))
        {
            AttachAuthHeaders(req);

            var tcs = new TaskCompletionSource<UnityWebRequest>();
            req.SendWebRequest().completed += _ => tcs.TrySetResult(req);
            await tcs.Task;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[HttpApiClient] Refresh failed: {req.error}");
                return false;
            }

            SaveCookies(req);
            Debug.Log("[HttpApiClient] Refresh successful.");
            return true;
        }
    }
}
