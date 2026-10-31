using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Exceptional admin operations require a matching, durably applied server receipt.</summary>
public static class TrucoAdminOperationService
{
    [Serializable] public sealed class Command
    {
        public string action, tournamentId, championId, userId, reason, idempotencyKey;
        public int amount;
    }
    [Serializable] public sealed class Receipt
    {
        public string id, action, idempotencyKey, actorId, at, reason;
        public string tournamentId, championId, userId, ledgerTransactionId, status;
        public int amount, balanceAfter, refundedTotal;
        public bool applied, refundsApplied;
    }
    sealed class Pending { public Command command; public string body, storage; }
    static readonly Dictionary<string, Pending> PendingCommands = new Dictionary<string, Pending>();
    static readonly Dictionary<string, Task<bool>> InFlight = new Dictionary<string, Task<bool>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { PendingCommands.Clear(); InFlight.Clear(); }

    public static Task<bool> SetChampion(string tournamentId, string championId) => Execute("AdminSetChampion", new Command {
        action = "set_champion", tournamentId = tournamentId, championId = championId, reason = "admin-set-champion"
    });
    public static Task<bool> ForceClose(string tournamentId, string reason) => Execute("AdminForceCloseTournament", new Command {
        action = "force_close_tournament", tournamentId = tournamentId, reason = reason
    });
    public static Task<bool> Credit(string userId, int amount, string reason) => Execute("AddUserCoins", new Command {
        action = "credit_coins", userId = userId, amount = amount, reason = reason
    });

    static void ValidateCommand(Command command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.reason)) throw new InvalidOperationException("Hace falta un motivo de administración.");
        if (command.action == "set_champion" && !string.IsNullOrWhiteSpace(command.tournamentId) && !string.IsNullOrWhiteSpace(command.championId)) return;
        if (command.action == "force_close_tournament" && !string.IsNullOrWhiteSpace(command.tournamentId)) return;
        if (command.action == "credit_coins" && !string.IsNullOrWhiteSpace(command.userId) && command.amount > 0) return;
        throw new InvalidOperationException("Los datos de la operación de administración están incompletos.");
    }

    static async Task<bool> Execute(string endpointKey, Command proposed)
    {
        try
        {
            ValidateCommand(proposed);
            int generation = HttpApiClient.SessionGeneration;
            string actorId = ApiController.GetSessionUser?.Data?._id;
            if (string.IsNullOrWhiteSpace(actorId)) return false;
            // The same unresolved intent retains its exact key/body. A confirmed new user intent may generate a new key.
            string storage = IntentStorage(actorId, Serialize(proposed));
            string context = generation.ToString(CultureInfo.InvariantCulture) + ":" + storage;
            if (InFlight.TryGetValue(context, out var existing)) return await existing;
            var completion = new TaskCompletionSource<bool>();
            InFlight[context] = completion.Task;
            _ = Send(context, storage, endpointKey, generation, proposed, completion);
            return await completion.Task;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AdminOperation] Request unconfirmed: " + ex.GetType().Name);
            return false;
        }
    }

    static async Task Send(string context, string storage, string endpointKey, int generation, Command proposed, TaskCompletionSource<bool> completion)
    {
        bool confirmed = false;
        try
        {
            if (!await ApiController.CheckUserAdmin() || generation != HttpApiClient.SessionGeneration) return;
            if (!PendingCommands.TryGetValue(context, out var pending))
            {
                proposed.idempotencyKey = PlayerPrefs.GetString(storage, "");
                if (string.IsNullOrEmpty(proposed.idempotencyKey))
                {
                    proposed.idempotencyKey = "admin-" + Guid.NewGuid().ToString("N");
                    // Persist only a random operation key under a hashed intent; no reason/user/payment data is stored here.
                    PlayerPrefs.SetString(storage, proposed.idempotencyKey); PlayerPrefs.Save();
                }
                pending = new Pending { command = proposed, body = Serialize(proposed), storage = storage };
                PendingCommands[context] = pending;
            }
            string id = pending.command.action == "credit_coins" ? pending.command.userId : pending.command.tournamentId;
            string route = ApiConfig.Custom(endpointKey, Uri.EscapeDataString(id));
            if (string.IsNullOrWhiteSpace(route)) return;
            string json = await HttpApiClient.PostAsync(route, pending.body);
            ParseReceipt(json, pending.command);
            confirmed = true;
            PendingCommands.Remove(context);
            try { PlayerPrefs.DeleteKey(pending.storage); PlayerPrefs.Save(); }
            catch (Exception ex) { Debug.LogWarning("[AdminOperation] Confirmed operation key cleanup failed: " + ex.GetType().Name); }
        }
        catch (Exception ex) { Debug.LogWarning("[AdminOperation] Result unconfirmed: " + ex.GetType().Name); }
        finally { InFlight.Remove(context); completion.TrySetResult(confirmed); }
    }

    static string IntentStorage(string actorId, string payload)
    {
        using (var hash = SHA256.Create())
            return "TrucoAdminIntent_v1_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(actorId + "\n" + payload))).Replace("-", "");
    }
    static string Serialize(Command command) => JsonConvert.SerializeObject(command,
        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore });

    public static Receipt ParseReceipt(string json, Command expected)
    {
        ValidateCommand(expected);
        if (string.IsNullOrWhiteSpace(expected.idempotencyKey)) throw new InvalidOperationException("Falta la clave de la operación.");
        var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        var data = root?["data"] as JObject;
        var receipt = data?["operation"] as JObject;
        if (!True(root?["success"]) || False(root?["ok"]) || False(data?["success"]) || False(data?["ok"])
            || receipt == null || !True(receipt["applied"])) throw new InvalidOperationException("El servidor no confirmó una operación aplicada.");
        foreach (string field in new[] { "id", "action", "idempotencyKey", "actorId", "at", "reason" }) RequireText(receipt, field);
        if (Text(receipt, "action") != expected.action || Text(receipt, "idempotencyKey") != expected.idempotencyKey
            || Text(receipt, "reason") != expected.reason
            || !DateTimeOffset.TryParse(Text(receipt, "at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new InvalidOperationException("El comprobante no corresponde a esta operación.");
        if (expected.action == "credit_coins")
        {
            if (Text(receipt, "userId") != expected.userId || Integer(receipt, "amount") != expected.amount)
                throw new InvalidOperationException("El comprobante no corresponde al usuario o importe solicitado.");
            Integer(receipt, "balanceAfter"); RequireText(receipt, "ledgerTransactionId");
        }
        else
        {
            if (Text(receipt, "tournamentId") != expected.tournamentId) throw new InvalidOperationException("El comprobante corresponde a otro torneo.");
            if (expected.action == "set_champion" && Text(receipt, "championId") != expected.championId)
                throw new InvalidOperationException("El servidor no confirmó el campeón solicitado.");
            if (expected.action == "force_close_tournament")
            {
                if (Text(receipt, "status") != "closed" || !True(receipt["refundsApplied"]))
                    throw new InvalidOperationException("El servidor no confirmó el cierre y los reintegros.");
                Integer(receipt, "refundedTotal");
            }
        }
        return receipt.ToObject<Receipt>();
    }

    static bool True(JToken value) => value?.Type == JTokenType.Boolean && value.Value<bool>();
    static bool False(JToken value) => value?.Type == JTokenType.Boolean && !value.Value<bool>();
    static string Text(JObject value, string key) => value?[key]?.Type == JTokenType.String ? value[key].Value<string>() : null;
    static void RequireText(JObject value, string key)
    {
        if (string.IsNullOrWhiteSpace(Text(value, key))) throw new InvalidOperationException("El comprobante está incompleto.");
    }
    static int Integer(JObject value, string key)
    {
        var token = value?[key];
        if (token?.Type != JTokenType.Integer || !int.TryParse(token.ToString(Formatting.None), NumberStyles.None,
                CultureInfo.InvariantCulture, out int amount)) throw new InvalidOperationException("El comprobante tiene un importe inválido.");
        return amount;
    }
}
