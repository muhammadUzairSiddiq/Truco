using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Adapter boundary for the manual wallet workflow. No default/unverified API routes.</summary>
public static class TrucoWalletService
{
    [Serializable] public sealed class Configuration
    {
        public int minimumBuy, minimumSell, maximumAmount;
        public int[] buyPresets, sellPresets;
        public bool identificationRequired;
    }
    [Serializable] public sealed class Request
    {
        public string id, type, status, createdAt, bank, alias, holder, whatsapp, receiptUrl;
        public int amount;
        public List<Stage> history = new List<Stage>();
    }
    [Serializable] public sealed class Stage { public string status, at; }
    [Serializable] public sealed class Snapshot
    {
        public int available, held;
        public Configuration configuration;
        public List<Request> requests = new List<Request>();
    }
    [Serializable] public sealed class Submission
    {
        public string type, bank, alias, holder, identification, whatsapp, idempotencyKey;
        public int amount;
    }
    public static string Validate(Submission value, Configuration config, int available)
    {
        if (value == null || config == null) return "No se pudo verificar la configuración del servicio.";
        if (value.type != "buy" && value.type != "sell") return "Operación inválida.";
        int minimum = value.type == "buy" ? config.minimumBuy : config.minimumSell;
        if (minimum <= 0 || config.maximumAmount < minimum) return "El servicio no confirmó los límites de la operación.";
        if (value.amount < minimum || value.amount > config.maximumAmount) return $"Ingresá entre {minimum} y {config.maximumAmount} Trucoins.";
        if (value.type == "sell")
        {
            if (value.amount > available) return "Saldo disponible insuficiente.";
            if (string.IsNullOrWhiteSpace(value.bank) || string.IsNullOrWhiteSpace(value.alias)
                || string.IsNullOrWhiteSpace(value.holder) || string.IsNullOrWhiteSpace(value.whatsapp)
                || (config.identificationRequired && string.IsNullOrWhiteSpace(value.identification)))
                return "Completá los datos de cobro.";
        }
        return null;
    }
    static string Route(string key, params object[] args)
    {
        string url = ApiConfig.Custom(key, args);
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Este servicio todavía no está disponible. Volvé a intentarlo más tarde.");
        return url;
    }
    static JObject Data(string json)
    {
        if (!TrucoServerAcknowledgement.Accepted(json)) throw new InvalidOperationException("El servidor no confirmó la operación.");
        var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        var data = root["data"] as JObject;
        if (data == null || data["success"]?.Value<bool>() == false || data["ok"]?.Type == JTokenType.Boolean && !data["ok"].Value<bool>()) throw new InvalidOperationException("Respuesta incompleta del servidor.");
        return data;
    }
    static int Integer(JObject data, string key, int minimum)
    {
        var token = data?[key];
        if (token?.Type != JTokenType.Integer || !long.TryParse(token.ToString(), out long value) || value < minimum || value > int.MaxValue)
            throw new InvalidOperationException("El servidor devolvió un importe o límite inválido.");
        return (int)value;
    }
    static Request RequestData(JObject data, string expectedId = null, Submission submission = null)
    {
        if (data == null || data["id"]?.Type != JTokenType.String || data["type"]?.Type != JTokenType.String
            || data["status"]?.Type != JTokenType.String || data["createdAt"]?.Type != JTokenType.String || data["history"] is not JArray)
            throw new InvalidOperationException("El servidor no confirmó los datos y el historial de la solicitud.");
        Integer(data, "amount", 1);
        var value = data.ToObject<Request>();
        if (string.IsNullOrWhiteSpace(value.id) || (value.type != "buy" && value.type != "sell") || string.IsNullOrWhiteSpace(value.status)
            || string.IsNullOrWhiteSpace(value.createdAt) || expectedId != null && value.id != expectedId
            || submission != null && (value.amount != submission.amount || value.type != submission.type))
            throw new InvalidOperationException("Solicitud pendiente de conciliación. Consultá el historial antes de volver a enviar.");
        foreach (var stage in (JArray)data["history"])
            if (stage is not JObject || stage["status"]?.Type != JTokenType.String || stage["at"]?.Type != JTokenType.String)
                throw new InvalidOperationException("El historial de la solicitud está incompleto.");
        return value;
    }
    public static Request ParseRequest(string json, string expectedId = null, Submission submission = null) => RequestData(Data(json), expectedId, submission);
    public static Snapshot ParseSnapshot(string json)
    {
        var data = Data(json); int available = Integer(data, "available", 0), held = Integer(data, "held", 0);
        var configuration = data["configuration"] as JObject;
        int minimumBuy = Integer(configuration, "minimumBuy", 1), minimumSell = Integer(configuration, "minimumSell", 1);
        int maximum = Integer(configuration, "maximumAmount", 1);
        if (maximum < minimumBuy || maximum < minimumSell || configuration["identificationRequired"]?.Type != JTokenType.Boolean)
            throw new InvalidOperationException("El servidor no confirmó límites coherentes de compra y venta.");
        int[] Presets(string key, int minimum)
        {
            if (configuration[key] is not JArray array) throw new InvalidOperationException("Faltan las opciones de importes del servicio.");
            var values = new List<int>();
            foreach (var token in array)
            {
                int value = Integer(new JObject { ["amount"] = token }, "amount", minimum);
                if (value > maximum) throw new InvalidOperationException("Una opción de importe supera el límite del servidor.");
                values.Add(value);
            }
            return values.ToArray();
        }
        var config = new Configuration { minimumBuy = minimumBuy, minimumSell = minimumSell, maximumAmount = maximum,
            identificationRequired = configuration["identificationRequired"].Value<bool>(), buyPresets = Presets("buyPresets", minimumBuy), sellPresets = Presets("sellPresets", minimumSell) };
        if (data["requests"] is not JArray requests) throw new InvalidOperationException("El servidor no devolvió el historial de solicitudes.");
        var result = new Snapshot { available = available, held = held, configuration = config };
        foreach (var request in requests) result.requests.Add(RequestData(request as JObject));
        return result;
    }
    public static async Task<Snapshot> Refresh()
    {
        string json = await HttpApiClient.GetAsync(Route("WalletSnapshot"));
        return ParseSnapshot(json);
    }
    public static async Task<Request> Submit(Submission submission)
    {
        if (submission == null || string.IsNullOrWhiteSpace(submission.idempotencyKey)) throw new InvalidOperationException("Falta la clave de la solicitud.");
        return ParseRequest(await HttpApiClient.PostAsync(Route("WalletCreateRequest"), JsonConvert.SerializeObject(submission)), submission: submission);
    }
    public static async Task<Request> Status(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Falta el identificador de la solicitud.");
        return ParseRequest(await HttpApiClient.GetAsync(Route("WalletRequestStatus", Uri.EscapeDataString(id))), id);
    }
    public static string StatusLabel(string status)
    {
        switch (status)
        {
            case "created": case "requested": return "Solicitada";
            case "receipt_sent": return "Comprobante enviado";
            case "verification": case "pending": return "En revisión";
            case "approved": return "Aprobada";
            case "paid": return "Pagada";
            case "credited": return "Acreditada";
            case "finalized": return "Finalizada";
            case "rejected": return "Rechazada";
            case "cancelled": return "Cancelada";
            default: return "Estado pendiente de confirmación";
        }
    }
}
