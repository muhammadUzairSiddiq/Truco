using Newtonsoft.Json.Linq;

/// <summary>Conservative interpretation of financial receipts; transport success is not settlement.</summary>
public static class TrucoServerAcknowledgement
{
    public static match TournamentMatch(string json)
    {
        var root = Parse(json);
        if (root == null || !Accepted(json)) return null;
        var data = root["data"] as JObject;
        if (data != null && Rejected(data)) return null;
        var payload = root["match"] as JObject ?? data?["match"] as JObject ?? data;
        if (payload == null || Rejected(payload)) return null;
        try
        {
            var result = payload.ToObject<match>();
            return string.IsNullOrWhiteSpace(result?._id) ? null : result;
        }
        catch { return null; }
    }

    public static bool Accepted(string json)
    {
        var root = Parse(json);
        return root != null && !Rejected(root)
            && (True(root["ok"]) || True(root["success"]));
    }

    public static bool RefundConfirmed(string json, string matchId)
    {
        var root = Parse(json);
        if (root == null || Rejected(root)) return false;
        var receipt = root["data"] as JObject ?? root;
        if (Rejected(receipt)) return false;
        string returnedId = (string)receipt["matchId"];
        if (!string.IsNullOrEmpty(returnedId) && returnedId != matchId) return false;
        return True(receipt["refunded"]) || True(receipt["alreadyRefunded"])
            || (string)receipt["status"] == "refunded";
    }

    public static bool PrizePaid(string json)
    {
        var root = Parse(json);
        if (root == null || Rejected(root)) return false;
        var receipt = root["data"] as JObject ?? root;
        return !Rejected(receipt) && (True(receipt["paid"]) || True(receipt["prizePaid"]));
    }

    static JObject Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JObject.Parse(json); } catch { return null; }
    }
    static bool True(JToken token) => token?.Type == JTokenType.Boolean && token.Value<bool>();
    static bool Rejected(JObject root) => root["ok"]?.Type == JTokenType.Boolean && !root["ok"].Value<bool>()
        || root["success"]?.Type == JTokenType.Boolean && !root["success"].Value<bool>();
}
