using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wipe ALL lobby matches on the server (admin force-close). Works outside Play Mode.
/// Window → Truco → Server Room Janitor
/// </summary>
public class TrucoServerRoomJanitorWindow : EditorWindow
{
    const string PrefEmail = "TrucoJanitor_Email";
    const string PrefPassword = "TrucoJanitor_Password";

    string _email = "";
    string _password = "";
    string _status = "Log in as admin, then force-close every active lobby match.";
    Vector2 _scroll;
    bool _busy;

    [MenuItem("Truco/Server Room Janitor (Wipe ALL Rooms)")]
    static void OpenWindow()
    {
        var w = GetWindow<TrucoServerRoomJanitorWindow>("Truco Janitor");
        w.minSize = new Vector2(420, 260);
        w.Show();
    }

    void OnEnable()
    {
        _email = EditorPrefs.GetString(PrefEmail, "");
        _password = EditorPrefs.GetString(PrefPassword, "");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Nuclear server cleanup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "GET /matches returns every player's rooms. Refresh as player 2 only deletes player 2's rows — " +
            "uzair's 'Sala Armando' stays until that account purges or you force-close here as admin.",
            MessageType.Info);

        _email = EditorGUILayout.TextField("Email", _email);
        _password = EditorGUILayout.PasswordField("Password", _password);

        EditorGUI.BeginDisabledGroup(_busy);
        if (GUILayout.Button("Login + Force-Close ALL Lobby Matches", GUILayout.Height(32)))
            _ = RunJanitorAsync();
        EditorGUI.EndDisabledGroup();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    async Task RunJanitorAsync()
    {
        if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
        {
            _status = "Enter admin email and password.";
            Repaint();
            return;
        }

        EditorPrefs.SetString(PrefEmail, _email.Trim());
        EditorPrefs.SetString(PrefPassword, _password);

        _busy = true;
        _status = "Logging in…";
        Repaint();

        try
        {
            string baseUrl = ApiConfig.BaseUrl?.TrimEnd('/') ?? "https://srv983121.hstgr.cloud/api";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            var loginBody = "{\"email\":\"" + EscapeJson(_email.Trim()) + "\",\"password\":\"" +
                            EscapeJson(_password) + "\"}";
            var loginResp = await http.PostAsync(baseUrl + "/auth/login",
                new StringContent(loginBody, Encoding.UTF8, "application/json"));
            string loginText = await loginResp.Content.ReadAsStringAsync();
            if (!loginResp.IsSuccessStatusCode)
            {
                _status = "Login failed (" + (int)loginResp.StatusCode + "): " + loginText;
                return;
            }

            string token = ExtractToken(loginText);
            if (string.IsNullOrEmpty(token))
            {
                _status = "Login OK but no token in response. Body: " + Truncate(loginText, 400);
                return;
            }

            http.DefaultRequestHeaders.Remove("Authorization");
            http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            _status = "Fetching matches…";
            Repaint();

            var listResp = await http.GetAsync(baseUrl + "/matches");
            string listText = await listResp.Content.ReadAsStringAsync();
            if (!listResp.IsSuccessStatusCode)
            {
                _status = "GET /matches failed: " + listText;
                return;
            }

            var ids = ExtractMatchIds(listText);
            var activeIds = ExtractActiveMatchIds(listText);
            if (activeIds.Count == 0 && ids.Count > 0)
                activeIds = ids;
            ids = activeIds;
            if (ids.Count == 0)
            {
                _status = "No matches on server. List response: " + Truncate(listText, 300);
                return;
            }

            int closed = 0;
            int failed = 0;
            var log = new StringBuilder();
            log.AppendLine("Force-closing " + ids.Count + " matches…");

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var closeResp = await http.PostAsync(baseUrl + "/matches/" + id + "/force-close",
                    new StringContent("{}", Encoding.UTF8, "application/json"));
                string closeText = await closeResp.Content.ReadAsStringAsync();
                if (closeResp.IsSuccessStatusCode)
                {
                    closed++;
                    log.AppendLine("OK  " + id);
                }
                else
                {
                    // Fallback: /end then /leave
                    bool ok = false;
                    var endResp = await http.PostAsync(baseUrl + "/matches/" + id + "/end",
                        new StringContent("{}", Encoding.UTF8, "application/json"));
                    if (endResp.IsSuccessStatusCode) ok = true;
                    else
                    {
                        var leaveResp = await http.PostAsync(baseUrl + "/matches/" + id + "/leave",
                            new StringContent("{}", Encoding.UTF8, "application/json"));
                        ok = leaveResp.IsSuccessStatusCode;
                    }

                    if (ok)
                    {
                        closed++;
                        log.AppendLine("end/leave OK " + id);
                    }
                    else
                    {
                        failed++;
                        log.AppendLine("FAIL " + id + ": " + Truncate(closeText, 120));
                    }
                }
            }

            log.AppendLine();
            log.AppendLine("Done. Closed " + closed + ", failed " + failed + ".");
            _status = log.ToString();
            Debug.Log("[Truco Janitor]\n" + _status);
        }
        catch (Exception ex)
        {
            _status = "Error: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
            Repaint();
        }
    }

    static string EscapeJson(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

    static string ExtractToken(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var resp = JsonUtility.FromJson<ApiController.LoginResponse>(json);
            if (resp != null)
            {
                if (!string.IsNullOrEmpty(resp.accessToken)) return resp.accessToken;
                if (!string.IsNullOrEmpty(resp.token)) return resp.token;
            }
        }
        catch { }

        const string key = "\"accessToken\":\"";
        int i = json.IndexOf(key, StringComparison.Ordinal);
        if (i >= 0)
        {
            i += key.Length;
            int j = json.IndexOf('"', i);
            if (j > i) return json.Substring(i, j - i);
        }
        return null;
    }

    static System.Collections.Generic.List<string> ExtractActiveMatchIds(string json)
    {
        var ids = new System.Collections.Generic.List<string>();
        try
        {
            var parsed = ApiController.TryParsePlayerMatchListJson(json);
            if (parsed == null) return ids;
            for (int i = 0; i < parsed.Count; i++)
            {
                var m = parsed[i];
                if (m == null || string.IsNullOrEmpty(m._id)) continue;
                if (!m.IsLobbyLikeStatus()) continue;
                ids.Add(m._id);
            }
        }
        catch { }
        return ids;
    }

    static System.Collections.Generic.List<string> ExtractMatchIds(string json)
    {
        var ids = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(json)) return ids;
        try
        {
            var parsed = ApiController.TryParsePlayerMatchListJson(json);
            if (parsed != null)
            {
                for (int i = 0; i < parsed.Count; i++)
                    if (!string.IsNullOrEmpty(parsed[i]?._id))
                        ids.Add(parsed[i]._id);
            }
        }
        catch { }

        if (ids.Count > 0) return ids;

        int idx = 0;
        while ((idx = json.IndexOf("\"_id\"", idx, StringComparison.Ordinal)) >= 0)
        {
            idx = json.IndexOf(':', idx);
            if (idx < 0) break;
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) break;
            int end = json.IndexOf('"', idx + 1);
            if (end > idx + 1)
            {
                string id = json.Substring(idx + 1, end - idx - 1);
                if (id.Length >= 12 && !ids.Contains(id)) ids.Add(id);
            }
            idx = end + 1;
        }
        return ids;
    }
}
