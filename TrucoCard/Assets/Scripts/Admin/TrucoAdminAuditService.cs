using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Read-only, server-authorized durable administrative audit feed.</summary>
public static class TrucoAdminAuditService
{
    [Serializable] public sealed class Entry { public string id, at, actorId, action, targetType, targetId, reason, detail; }
    [Serializable] public sealed class Page { public List<Entry> items = new List<Entry>(); public string nextCursor; }
    public static Page Parse(string json)
    {
        if (!TrucoServerAcknowledgement.Accepted(json)) throw new InvalidOperationException("El servidor no confirmó acceso al registro de auditoría.");
        var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        var data = root?["data"] as JObject;
        if (data?["success"]?.Type == JTokenType.Boolean && !data["success"].Value<bool>()
            || data?["ok"]?.Type == JTokenType.Boolean && !data["ok"].Value<bool>())
            throw new InvalidOperationException("El servidor rechazó la consulta de auditoría.");
        if (data?["items"] is not JArray items || data["nextCursor"] != null && data["nextCursor"].Type != JTokenType.String && data["nextCursor"].Type != JTokenType.Null)
            throw new InvalidOperationException("El servidor devolvió un registro de auditoría incompleto.");
        var result = new Page { nextCursor = (string)data["nextCursor"] };
        foreach (var token in items)
        {
            if (token is not JObject row) throw new InvalidOperationException("Evento de auditoría inválido.");
            foreach (string key in new[] { "id", "at", "actorId", "action" })
                if (row[key]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)row[key]))
                    throw new InvalidOperationException("Faltan identidad, fecha o acción del evento de auditoría.");
            result.items.Add(row.ToObject<Entry>());
        }
        return result;
    }
    public static async Task<Page> List(string cursor = null, int limit = 5)
    {
        if (!await ApiController.CheckUserAdmin()) throw new InvalidOperationException("El servidor no confirmó permisos para consultar auditoría.");
        string url = ApiConfig.Custom("AdminAudit");
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("La auditoría del servidor todavía no está disponible.");
        limit = Math.Max(1, Math.Min(100, limit));
        string query = "limit=" + limit + (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
        return Parse(await HttpApiClient.GetAsync(url + (url.Contains("?") ? "&" : "?") + query));
    }
}
