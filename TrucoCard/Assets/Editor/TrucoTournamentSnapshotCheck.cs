using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class TrucoTournamentSnapshotCheck : EditorWindow
{
    [SerializeField] string _tournamentId = "";
    string _report = "";
    bool _checking;
    Vector2 _scroll;

    [MenuItem("Truco/Check Live Tournament Snapshot")]
    static void Open() => GetWindow<TrucoTournamentSnapshotCheck>("Tournament Snapshot", typeof(SceneView));

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Enter Play Mode and sign in with a test account. This check reads the tournament without entering it.", MessageType.Info);
        _tournamentId = EditorGUILayout.TextField("Tournament ID", _tournamentId);
        var localId = ApiController.GetSessionUser?.Data?._id;
        using (new EditorGUI.DisabledScope(_checking || !Application.isPlaying || string.IsNullOrWhiteSpace(localId)
            || string.IsNullOrWhiteSpace(_tournamentId)))
        {
            if (GUILayout.Button("Check snapshot")) Check(_tournamentId.Trim(), localId);
        }
        if (string.IsNullOrEmpty(_report)) return;
        if (GUILayout.Button("Copy redacted report")) EditorGUIUtility.systemCopyBuffer = _report;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(_report, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(240), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    async void Check(string tournamentId, string localId)
    {
        _checking = true;
        long status = 0;
        string body = null;
        string path = "tournament snapshot";
        string outcome;
        try
        {
            string url = ApiConfig.Custom("TournamentSnapshot", Uri.EscapeDataString(tournamentId));
            if (!Uri.TryCreate(url, UriKind.Absolute, out var route)) throw new InvalidOperationException();
            path = route.AbsolutePath;
            var json = await HttpApiClient.GetForDiagnosticsAsync(url, (code, response) => { status = code; body = response; });
            var snapshot = TrucoTournamentService.ReadReceipt(json, tournamentId, localId);
            outcome = $"Unity accepted the snapshot (revision {snapshot.revision}).";
        }
        catch (InvalidOperationException exception)
        {
            outcome = "Unity validation failed. " + (exception.InnerException?.Message ?? "Check the tournament route configuration.");
        }
        catch (Exception)
        {
            outcome = "The request did not complete successfully.";
        }
        finally { _checking = false; }

        _report = $"GET {path}\nHTTP {(status > 0 ? status.ToString() : "unavailable")}\n{outcome}\n\n{Redact(body)}";
        Repaint();
    }

    static string Redact(string body)
    {
        if (string.IsNullOrEmpty(body)) return "No response body captured.";
        try
        {
            var json = JsonConvert.DeserializeObject<JToken>(body, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            if (json == null) return "Empty JSON response.";
            if (json is not JObject && json is not JArray) return $"Response root was {json.Type}. Expected a JSON object.";
            RedactValues(json);
            return json.ToString(Formatting.Indented);
        }
        catch (JsonException) { return "The response is not valid JSON."; }
    }

    static void RedactValues(JToken token)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                string field = property.Name.ToLowerInvariant();
                if (field.Contains("token") || field.Contains("secret") || field.Contains("password") || field.Contains("cookie")
                    || field.Contains("authorization") || field.Contains("credential"))
                    property.Value = "[redacted]";
                else if (property.Value.Type == JTokenType.String && IsContractLabel((string)property.Value))
                    continue;
                else
                    RedactValues(property.Value);
            }
        }
        else if (token is JArray array)
        {
            for (int i = 0; i < array.Count; i++) RedactValues(array[i]);
        }
        else if (token.Type == JTokenType.String)
            token.Replace(new JValue("[redacted]"));
    }

    static bool IsContractLabel(string value) => value is "registration" or "starting" or "playing" or "completed"
        or "cancelled" or "reconciliation_required" or "not_registered" or "registered" or "waiting" or "eliminated"
        or "withdrawn" or "champion" or "ready" or "finished" or "pending" or "refunded" or "paid" or "not_due" or "TRUCOINS";
}
