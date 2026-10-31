using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Loads compatible route updates from a trusted, fixed HTTPS bootstrap before login.</summary>
public static class TrucoRemoteApiConfiguration
{
    public const int ClientContractVersion = 1;
    [Serializable] public sealed class Payload
    {
        public int schemaVersion, revision, minimumClientContractVersion;
        public string apiBaseUrl;
        public TrucoApiEndpointsSO.Endpoint[] endpoints;
    }
    [Serializable] public sealed class Envelope { public bool success; public Payload data; }
    static TrucoApiEndpointsSO _settings;
    static Task _loading;
    static int _revision = -1;
    public static bool RequiresClientUpdate { get; private set; }
    public static int AppliedRevision => _revision;
    static string CacheKey => "TrucoRoutesCacheV1:" + _settings.remoteConfigurationUrl;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _settings = null; _loading = null; _revision = -1; RequiresClientUpdate = false; }
    public static void Initialize(TrucoApiEndpointsSO settings)
    {
        _settings = settings; _loading = null; _revision = settings == null ? -1 : Mathf.Max(1, settings.remoteConfigurationRevision); RequiresClientUpdate = false;
        if (settings == null || !settings.fetchRemoteConfiguration) return;
        string cached = PlayerPrefs.GetString(CacheKey, "");
        if (!string.IsNullOrEmpty(cached)) TryApply(cached, false);
    }
    public static Task EnsureLoadedAsync()
    {
        if (_settings == null)
        {
            var settings = Resources.Load<TrucoApiEndpointsSO>("TrucoApiEndpoints");
            if (settings != null) Initialize(settings);
        }
        return _loading ?? (_loading = LoadAsync());
    }
    static async Task LoadAsync()
    {
        if (_settings == null || !_settings.fetchRemoteConfiguration) return;
        if (!ValidHttps(_settings.remoteConfigurationUrl, out _))
        { Debug.LogWarning("[API configuration] Invalid HTTPS bootstrap; using bundled routes."); return; }
        try
        {
            using (var request = UnityWebRequest.Get(_settings.remoteConfigurationUrl))
            {
                request.timeout = Mathf.Clamp(_settings.configurationTimeoutSeconds, 2, 20);
                request.redirectLimit = 0;
                request.SetRequestHeader("Accept", "application/json");
                // Bootstrap is public configuration: never attach cookies, bearer tokens or game secrets.
                var completion = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => completion.TrySetResult(true);
                await completion.Task;
                if (request.result != UnityWebRequest.Result.Success) return;
                string json = request.downloadHandler.text;
                if (json == null || json.Length > 131072) return;
                if (!TryApply(json, true)) Debug.LogWarning("[API configuration] Incompatible/invalid configuration; last valid routes retained.");
            }
        }
        catch (Exception exception)
        { Debug.LogWarning("[API configuration] Bootstrap unavailable (" + exception.GetType().Name + "); last valid routes retained."); }
    }
    static bool TryApply(string json, bool persist)
    {
        Envelope envelope;
        try { envelope = JsonUtility.FromJson<Envelope>(json); }
        catch (ArgumentException) { return false; }
        if (envelope == null || !envelope.success || envelope.data == null) return false;
        var data = envelope.data;
        if (!ValidateCore(data, _settings, _revision, true, out _)) return false;
        if (data.minimumClientContractVersion > ClientContractVersion)
        { RequiresClientUpdate = true; return false; }
        if (!Validate(data, _settings, _revision, out _)) return false;
        ApiConfig.ApplyRemoteEndpoints(data.apiBaseUrl, data.endpoints);
        _revision = data.revision;
        RequiresClientUpdate = false;
        if (persist) { PlayerPrefs.SetString(CacheKey, json); PlayerPrefs.Save(); }
        return true;
    }
    public static bool Validate(Payload data, TrucoApiEndpointsSO settings, int currentRevision, out string error)
        => ValidateCore(data, settings, currentRevision, false, out error);
    static bool ValidateCore(Payload data, TrucoApiEndpointsSO settings, int currentRevision, bool permitUpgradeNotice, out string error)
    {
        error = null;
        if (data == null || settings == null || data.schemaVersion != 1 || data.revision < 1 || data.revision < currentRevision
            || data.minimumClientContractVersion < 1 || (!permitUpgradeNotice && data.minimumClientContractVersion > ClientContractVersion))
        { error = "Unsupported contract/schema or stale revision."; return false; }
        if (!ValidHttps(data.apiBaseUrl, out var proposed) || !string.IsNullOrEmpty(proposed.Query)
            || !OriginAllowed(proposed, settings))
        { error = "API origin is not trusted by this build."; return false; }
        if (data.endpoints == null || data.endpoints.Length == 0 || data.endpoints.Length > 256)
        { error = "An endpoint catalog is required."; return false; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in data.endpoints)
        {
            if (endpoint == null || string.IsNullOrEmpty(endpoint.key) || !Regex.IsMatch(endpoint.key, "^[A-Za-z][A-Za-z0-9]{0,79}$")
                || !keys.Add(endpoint.key) || !ValidPath(endpoint.pathTemplate)
                || (endpoint.method != "GET" && endpoint.method != "POST" && endpoint.method != "PUT" && endpoint.method != "PATCH" && endpoint.method != "DELETE"))
            { error = "Invalid, duplicate or unsafe endpoint entry."; return false; }
            var expected = settings.endpoints?.FirstOrDefault(item => item != null && item.key == endpoint.key);
            if (!string.IsNullOrEmpty(expected?.method) && expected.method != endpoint.method)
            { error = "HTTP method changes require a compatible client contract."; return false; }
            if (expected != null && !string.IsNullOrEmpty(expected.pathTemplate)
                && expected.pathTemplate.Contains("{0}") != endpoint.pathTemplate.Contains("{0}"))
            { error = "Endpoint ID placeholders must preserve the bundled client contract."; return false; }
        }
        return true;
    }
    public static bool ValidPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 2048 || !path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal) || path.Any(char.IsWhiteSpace) || path.Any(char.IsControl)) return false;
        string decoded;
        try { decoded = Uri.UnescapeDataString(path); } catch (UriFormatException) { return false; }
        if (decoded.Contains("\\") || decoded.Contains("://") || decoded.Contains("..") || decoded.Contains("#")) return false;
        return Regex.Matches(path, "\\{[^}]*\\}").Cast<Match>().All(match => match.Value == "{0}")
            && Regex.Replace(path, "\\{0\\}", "").IndexOfAny(new[] { '{', '}' }) < 0;
    }
    static bool ValidHttps(string url, out Uri uri) => Uri.TryCreate(url, UriKind.Absolute, out uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
    static bool OriginAllowed(Uri proposed, TrucoApiEndpointsSO settings)
    {
        var candidates = new List<string> { settings.baseUrl };
        if (settings.additionalTrustedApiOrigins != null) candidates.AddRange(settings.additionalTrustedApiOrigins);
        return candidates.Any(value => ValidHttps(value, out var candidate)
            && candidate.GetLeftPart(UriPartial.Authority).Equals(proposed.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
    }
}
