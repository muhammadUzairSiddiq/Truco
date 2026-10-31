using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Explicit proposed admin wallet contract. Every call requires server-verified admin access.</summary>
public static class TrucoWalletAdminService
{
    [Serializable] public sealed class Owner { public string id, username; }
    [Serializable] public sealed class AuditEntry
    {
        public string id, action, actorId, at, reason, idempotencyKey;
        public int revision;
    }
    [Serializable] public sealed class Operation
    {
        public string id, requestId, action, idempotencyKey;
        public int revision;
        public bool applied;
    }
    [Serializable] public sealed class Record
    {
        public TrucoWalletService.Request request;
        public Owner owner;
        public int revision;
        public string[] allowedActions;
        public string receiptDownloadUrl;
        public List<AuditEntry> audit = new List<AuditEntry>();
        public Operation operationReceipt;
    }
    [Serializable] public sealed class Page
    {
        public List<Record> items = new List<Record>();
        public string nextCursor;
    }
    [Serializable] public sealed class Command
    {
        public int expectedRevision;
        public string reason, idempotencyKey, paymentReference;
    }
    [Serializable] public sealed class Mutation { public Record record; public Operation operation; }

    static async Task RequireAdmin()
    {
        if (!await ApiController.CheckUserAdmin()) throw new InvalidOperationException("El servidor no confirmó permisos de administrador.");
    }
    static string Route(string key, string id = null)
    {
        string route = id == null ? ApiConfig.Custom(key) : ApiConfig.Custom(key, Uri.EscapeDataString(id));
        if (string.IsNullOrWhiteSpace(route)) throw new InvalidOperationException("El servicio de administración de Trucoins todavía no está disponible.");
        return route;
    }
    static JObject Data(string json)
    {
        if (!TrucoServerAcknowledgement.Accepted(json)) throw new InvalidOperationException("El servidor no confirmó la operación.");
        var data = JObject.Parse(json)["data"] as JObject;
        if (data == null || data["success"]?.Value<bool>() == false || data["ok"]?.Type == JTokenType.Boolean && !data["ok"].Value<bool>()) throw new InvalidOperationException("Respuesta de administración incompleta.");
        return data;
    }
    static Record ValidateRecord(Record value, string expectedId = null)
    {
        var request = value?.request;
        if (request == null || string.IsNullOrWhiteSpace(request.id) || request.amount <= 0
            || (request.type != "buy" && request.type != "sell") || string.IsNullOrWhiteSpace(request.status)
            || string.IsNullOrWhiteSpace(value.owner?.id) || value.revision < 0
            || expectedId != null && request.id != expectedId)
            throw new InvalidOperationException("El servidor devolvió una solicitud incompleta o diferente.");
        return value;
    }
    static Record RecordFromPayload(JToken token, string expectedId = null)
    {
        var payload = token as JObject; var request = payload?["request"] as JObject;
        if (payload?["revision"]?.Type != JTokenType.Integer || request?["amount"]?.Type != JTokenType.Integer
            || request?["id"]?.Type != JTokenType.String || request?["type"]?.Type != JTokenType.String
            || request?["status"]?.Type != JTokenType.String || payload?["owner"]?["id"]?.Type != JTokenType.String)
            throw new InvalidOperationException("La solicitud no cumple el contrato de importes, identidad y revisión.");
        if (payload["operationReceipt"] != null && payload["operationReceipt"].Type != JTokenType.Null)
            RequireOperationPayload(payload["operationReceipt"]);
        return ValidateRecord(payload.ToObject<Record>(), expectedId);
    }
    static void RequireOperationPayload(JToken token)
    {
        if (token is not JObject operation || operation["applied"]?.Type != JTokenType.Boolean || !operation["applied"].Value<bool>()
            || operation["revision"]?.Type != JTokenType.Integer
            || new[] { "id", "requestId", "action", "idempotencyKey" }.Any(key => operation[key]?.Type != JTokenType.String))
            throw new InvalidOperationException("No se recibió un comprobante válido de operación aplicada.");
    }
    public static Page ParsePage(string json)
    {
        var data = Data(json);
        if (data["items"] is not JArray) throw new InvalidOperationException("El servidor no devolvió la lista de solicitudes.");
        return new Page { items = data["items"].Select(item => RecordFromPayload(item)).ToList(), nextCursor = (string)data["nextCursor"] };
    }
    public static Record ParseRecord(string json, string expectedId) => RecordFromPayload(Data(json), expectedId);
    public static string ValidateCommand(Record record, string action, Command command)
    {
        if (record?.request == null || command == null) return "No hay una solicitud confirmada para modificar.";
        if (action != "approve" && action != "reject" && action != "pay") return "Acción inválida.";
        if (record.allowedActions == null || !record.allowedActions.Contains(action)) return "El servidor no permite esa acción en el estado actual.";
        if (command.expectedRevision != record.revision) return "La solicitud cambió. Actualizá antes de continuar.";
        if (string.IsNullOrWhiteSpace(command.idempotencyKey) || command.idempotencyKey.Length > 128) return "Falta la clave de la operación.";
        if (string.IsNullOrWhiteSpace(command.reason) || command.reason.Trim().Length < 3 || command.reason.Length > 500) return "Ingresá un motivo de entre 3 y 500 caracteres.";
        if (action == "pay" && (record.request.type != "sell" || string.IsNullOrWhiteSpace(command.paymentReference)
            || command.paymentReference.Trim().Length < 3 || command.paymentReference.Length > 200))
            return "El pago de una venta requiere la referencia de la transferencia.";
        return null;
    }
    public static bool MatchesOperation(Operation operation, string id, string action, Command command) =>
        command != null && operation != null && operation.applied && !string.IsNullOrWhiteSpace(operation.id)
        && operation.requestId == id && operation.action == action && operation.idempotencyKey == command?.idempotencyKey
        && operation.revision > command.expectedRevision;
    public static Mutation ParseMutation(string json, Record previous, string action, Command command)
    {
        if (previous?.request == null || previous.owner == null || command == null)
            throw new InvalidOperationException("No hay una operación pendiente para conciliar.");
        var data = Data(json);
        RequireOperationPayload(data["operation"]);
        var record = RecordFromPayload(data["record"], previous.request.id);
        var value = new Mutation { record = record, operation = data["operation"].ToObject<Operation>() };
        if (record.owner.id != previous.owner.id || record.request.type != previous.request.type
            || record.request.amount != previous.request.amount || !MatchesOperation(value.operation, record.request.id, action, command)
            || record.revision < value.operation.revision)
            throw new InvalidOperationException("La respuesta no confirmó esta operación. Consultá la solicitud antes de reintentar.");
        bool expectedStatus = action == "reject" ? record.request.status == "rejected"
            : action == "pay" ? record.request.type == "sell" && (record.request.status == "paid" || record.request.status == "finalized")
            : record.request.type == "buy" ? record.request.status == "credited" : record.request.status == "approved";
        if (!expectedStatus) throw new InvalidOperationException("El servidor no confirmó el estado final esperado de la operación.");
        return value;
    }
    public static async Task<Page> List(string cursor = null, string type = null)
    {
        await RequireAdmin();
        string url = Route("AdminWalletRequestList");
        var query = new List<string> { "limit=20" };
        if (!string.IsNullOrEmpty(cursor)) query.Add("cursor=" + Uri.EscapeDataString(cursor));
        if (type == "buy" || type == "sell") query.Add("type=" + type);
        return ParsePage(await HttpApiClient.GetAsync(url + (url.Contains("?") ? "&" : "?") + string.Join("&", query)));
    }
    public static async Task<Record> Detail(string id, string operationKey = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Falta el identificador de la solicitud.");
        await RequireAdmin(); string url = Route("AdminWalletRequestDetail", id);
        if (!string.IsNullOrEmpty(operationKey)) url += (url.Contains("?") ? "&" : "?") + "operationKey=" + Uri.EscapeDataString(operationKey);
        return ParseRecord(await HttpApiClient.GetAsync(url), id);
    }
    public static async Task<Mutation> Submit(Record record, string action, Command command)
    {
        string error = ValidateCommand(record, action, command); if (error != null) throw new InvalidOperationException(error);
        await RequireAdmin();
        string key = action == "approve" ? "AdminWalletRequestApprove" : action == "reject" ? "AdminWalletRequestReject" : "AdminWalletRequestPay";
        return ParseMutation(await HttpApiClient.PostAsync(Route(key, record.request.id), JsonConvert.SerializeObject(command)), record, action, command);
    }
}
