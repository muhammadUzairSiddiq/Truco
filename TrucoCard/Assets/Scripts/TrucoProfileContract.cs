using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Accepts a profile only when the server explicitly supplies its identity and balance.</summary>
public static class TrucoProfileContract
{
    public static bool TryParse(string json, out User user)
    {
        user = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var envelope = JsonConvert.DeserializeObject<JObject>(json,
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            if (envelope?["success"]?.Type != JTokenType.Boolean || !envelope["success"].Value<bool>()) return false;
            var profile = envelope["user"] as JObject;
            if (profile == null) return false;
            string id = Identity(profile["_id"]) ?? Identity(profile["id"]);
            if (id == null) return false;
            var balance = (profile["wallet"] as JObject)?["balance"];
            if (balance?.Type != JTokenType.Integer ||
                !int.TryParse(balance.ToString(Formatting.None), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int amount)) return false;

            var parsed = profile.ToObject<User>();
            if (parsed == null || parsed.wallet == null) return false;
            parsed._id = id;
            parsed.wallet.balance = amount;
            user = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            return false;
        }
    }

    static string Identity(JToken value) => value?.Type == JTokenType.String &&
        !string.IsNullOrWhiteSpace(value.Value<string>()) ? value.Value<string>() : null;
}
