using System;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Wallet hub, amount selection, payout form, request details and history.</summary>
public sealed class TrucoWalletScreen : MonoBehaviour
{
    static TrucoWalletScreen _instance;
    RectTransform _safe, _body;
    TextMeshProUGUI _title, _notice;
    TrucoWalletService.Snapshot _snapshot;
    TrucoWalletService.Submission _submission;
    bool _busy;
    int _generation;
#if UNITY_EDITOR
    public static GameObject EditorPreview(int page, TrucoWalletService.Snapshot fixture, TrucoWalletService.Request request = null)
    {
        if (_instance != null) TrucoMenuTheme.DestroyUI(_instance.gameObject);
        var root = new GameObject("WalletEditorPreview"); _instance = root.AddComponent<TrucoWalletScreen>();
        _instance.Build(); _instance._snapshot = fixture;
        switch (page)
        {
            case 1: _instance.Form("buy"); break;
            case 2: _instance.Form("sell"); break;
            case 3: _instance.Details(request); break;
            case 4: _instance.History(); break;
            default: _instance.Hub(); break;
        }
        _instance._notice.text = "VISTA EDITOR · Datos de ejemplo";
        return root;
    }
#endif
    public static void Show()
    {
        if (_instance == null)
        {
            var go = new GameObject("TrucoinsWallet"); _instance = go.AddComponent<TrucoWalletScreen>();
            _instance.Build();
        }
        _instance.gameObject.SetActive(true); _instance.Hub(); _instance.Refresh();
    }
    void Build()
    {
        _safe = TrucoClubUI.Screen(gameObject, TrucoCanvasOrder.Screen);
        _title = TrucoClubUI.Text(_safe, "TRUCOINS", 48, TrucoClubUI.Gold, new Vector2(.06f, .88f), new Vector2(.94f, .98f), true);
        _notice = TrucoClubUI.Text(_safe, "", 23, TrucoClubUI.Cream, new Vector2(.06f, .75f), new Vector2(.94f, .88f));
        TrucoClubUI.Button(_safe, "VOLVER", () => { if (!_busy) { _generation++; gameObject.SetActive(false); } }, new Vector2(.06f, .02f), new Vector2(.48f, .09f), true);
        TrucoClubUI.Button(_safe, "ACTUALIZAR", Refresh, new Vector2(.52f, .02f), new Vector2(.94f, .09f), true);
    }
    void Clear(string title)
    {
        _generation++;
        _title.text = title;
        if (_body != null) TrucoMenuTheme.DestroyUI(_body.gameObject);
        _body = TrucoClubUI.Rect("Content", _safe, new Vector2(.06f, .11f), new Vector2(.94f, .75f));
    }
    async void Refresh()
    {
        if (_busy) return;
        _busy = true; int generation = _generation; _notice.text = "Consultando el saldo y las solicitudes…";
        try { var result = await TrucoWalletService.Refresh(); if (this == null || generation != _generation) return; _snapshot = result; _notice.text = "Saldo confirmado por el servidor"; Hub(); }
        catch (Exception ex) { if (this != null && generation == _generation) _notice.text = ex.Message; }
        finally { if (this != null) _busy = false; }
    }
    void Hub()
    {
        Clear("MI BILLETERA");
        var profile = ApiController.GetSessionUser?.Data?.wallet;
        string available = _snapshot != null ? _snapshot.available.ToString() : profile != null ? profile.balance.ToString() : "—";
        string held = _snapshot != null ? _snapshot.held.ToString() : "—";
        TrucoClubUI.Text(_body, "SALDO DISPONIBLE\n<size=64>" + available + "</size> Trucoins", 28, TrucoClubUI.Cream, new Vector2(0, .82f), Vector2.one, true);
        TrucoClubUI.Text(_body, "Saldo retenido: " + held + " Trucoins", 26, TrucoClubUI.Muted, new Vector2(0, .75f), new Vector2(1, .82f));
        TrucoClubUI.Button(_body, "COMPRAR TRUCOINS", () => Form("buy"), new Vector2(0, .62f), new Vector2(1, .73f));
        TrucoClubUI.Button(_body, "VENDER TRUCOINS", () => Form("sell"), new Vector2(0, .49f), new Vector2(1, .60f));
        TrucoClubUI.Button(_body, "HISTORIAL DE SOLICITUDES", History, new Vector2(0, .36f), new Vector2(1, .47f), true);
        TrucoClubUI.Text(_body, "ÚLTIMAS SOLICITUDES", 25, TrucoClubUI.Gold, new Vector2(0, .30f), new Vector2(1, .35f), true);
        var recent = _snapshot?.requests?.Where(request => request != null)
            .OrderByDescending(request => DateTimeOffset.TryParse(request.createdAt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var time) ? time.UtcDateTime.Ticks : long.MinValue)
            .Take(3).ToArray();
        if (recent == null || recent.Length == 0)
            TrucoClubUI.Text(_body, "No hay solicitudes para mostrar.", 25, TrucoClubUI.Muted, new Vector2(0, .08f), new Vector2(1, .28f));
        else for (int i = 0; i < recent.Length; i++)
        {
            var request = recent[i];
            string operation = request.type == "buy" ? "Compra" : request.type == "sell" ? "Venta" : "Solicitud";
            string at = DateTimeOffset.TryParse(request.createdAt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var time)
                ? time.ToLocalTime().ToString("dd/MM · HH:mm") : request.createdAt ?? "Sin hora confirmada";
            var row = TrucoClubUI.Button(_body,
                $"{operation}: {request.amount} · {TrucoWalletService.StatusLabel(request.status)}\n{at}",
                () => Details(request), new Vector2(0, .22f - i * .08f), new Vector2(1, .30f - i * .08f), true);
            row.name = "RecentRequest_" + request.id;
        }
        TrucoClubUI.Text(_body, "Saldos sujetos a confirmación del servidor.", 21, TrucoClubUI.Muted, Vector2.zero, new Vector2(1, .055f));
    }
    void Form(string type)
    {
        if (_busy) return;
        Clear(type == "buy" ? "COMPRAR TRUCOINS" : "VENDER TRUCOINS");
        bool sell = type == "sell";
        var config = _snapshot?.configuration;
        _submission = new TrucoWalletService.Submission { type = type, idempotencyKey = Guid.NewGuid().ToString("N") };
        int[] presets = config == null ? (sell ? new[] { 50, 100, 150, 200 } : new[] { 50, 60, 75, 100, 150, 200 }) : sell ? config.sellPresets : config.buyPresets;
        var amount = TrucoClubUI.Input(_body, "Otro monto", new Vector2(0, .72f), new Vector2(1, .81f));
        amount.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (presets != null) for (int i = 0; i < Math.Min(6, presets.Length); i++)
        {
            int columns = sell ? 4 : 3;
            int value = presets[i]; float x = (i % columns) / (float)columns, y = i < columns ? .91f : .82f;
            TrucoClubUI.Button(_body, value.ToString(), () => amount.text = value.ToString(), new Vector2(x, y), new Vector2(x + 1f / columns, y + .085f), true);
        }
        if (sell) TrucoClubUI.Text(_body, "Disponible: " + (_snapshot != null ? _snapshot.available.ToString() : "—") + " Trucoins", 26, TrucoClubUI.Muted, new Vector2(0, .82f), new Vector2(1, .9f));
        TMP_InputField bank = null, alias = null, holder = null, id = null, phone = null;
        if (sell)
        {
            bank = TrucoClubUI.Input(_body, "Banco", new Vector2(0, .61f), new Vector2(1, .70f));
            alias = TrucoClubUI.Input(_body, "Alias o cuenta", new Vector2(0, .51f), new Vector2(1, .60f));
            holder = TrucoClubUI.Input(_body, "Titular de la cuenta", new Vector2(0, .41f), new Vector2(1, .50f));
            id = TrucoClubUI.Input(_body, config != null && config.identificationRequired ? "Documento (obligatorio)" : "Documento (opcional)", new Vector2(0, .31f), new Vector2(1, .40f));
            phone = TrucoClubUI.Input(_body, "WhatsApp con código de país", new Vector2(0, .21f), new Vector2(1, .30f));
        }
        else TrucoClubUI.Text(_body, "Elegí la cantidad. Las instrucciones de pago se mostrarán después de crear tu solicitud.", 29, TrucoClubUI.Cream, new Vector2(0, .35f), new Vector2(1, .65f));
        var submit = TrucoClubUI.Button(_body, sell ? "SOLICITAR VENTA" : "CONTINUAR", async () =>
        {
            if (_busy) return;
            if (TrucoMaintenanceMode.BlocksTransactions)
            {
                _notice.text = TrucoMaintenanceMode.Message;
                return;
            }
            int.TryParse(amount.text, out _submission.amount);
            _submission.bank = bank?.text.Trim(); _submission.alias = alias?.text.Trim(); _submission.holder = holder?.text.Trim();
            _submission.identification = id?.text.Trim(); _submission.whatsapp = phone?.text.Trim();
            string error = TrucoWalletService.Validate(_submission, config, _snapshot?.available ?? 0);
            if (error != null) { _notice.text = error; return; }
            _busy = true; int generation = _generation; _notice.text = "Enviando solicitud…";
            try { var request = await TrucoWalletService.Submit(_submission); if (this != null && generation == _generation) Details(request); }
            catch (Exception ex) { if (this != null && generation == _generation) _notice.text = ex.Message; }
            finally { if (this != null) _busy = false; }
        }, new Vector2(0, .09f), new Vector2(1, .19f));
        submit.interactable = config != null;
        if (config == null) _notice.text = "No se pudieron verificar los límites. Actualizá para reintentar.";
        TrucoClubUI.Button(_body, "BILLETERA", Hub, Vector2.zero, new Vector2(1, .08f), true);
    }
    void History()
    {
        if (_busy) return;
        Clear("HISTORIAL");
        var requests = _snapshot?.requests;
        if (requests == null || requests.Count == 0) { TrucoClubUI.Text(_body, "No hay solicitudes para mostrar.", 30, TrucoClubUI.Muted, new Vector2(0, .4f), new Vector2(1, .7f)); return; }
        var scroll = TrucoClubUI.Panel("History", _body, Color.clear, Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", scroll.transform, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var content = TrucoClubUI.Rect("Rows", viewport, new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, requests.Count * 130);
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i]; if (r == null) continue;
            var button = TrucoClubUI.Button(content, $"{r.amount} TRUCOINS  ·  {TrucoWalletService.StatusLabel(r.status)}\n{r.id}  ·  {r.createdAt}", () => Details(r), Vector2.zero, Vector2.one, true);
            var rt = (RectTransform)button.transform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1); rt.sizeDelta = new Vector2(-16, 116); rt.anchoredPosition = new Vector2(0, -i * 130);
        }
    }
    void Details(TrucoWalletService.Request request)
    {
        Clear(request.type == "sell" ? "ESTADO DE VENTA" : "ESTADO DE COMPRA");
        _notice.text = request.id;
        TrucoClubUI.Text(_body, request.amount + " TRUCOINS\n" + TrucoWalletService.StatusLabel(request.status), 42, TrucoClubUI.Gold, new Vector2(0, .76f), Vector2.one, true);
        bool sale = request.type == "sell";
        if (sale) TrucoClubUI.Text(_body, "Retenido total de la billetera: " + (_snapshot != null ? _snapshot.held.ToString() : "—") + " Trucoins", 25,
            TrucoClubUI.Muted, new Vector2(0, .69f), new Vector2(1, .77f));
        TrucoClubUI.Text(_body, $"Banco: {request.bank ?? "—"}\nAlias / cuenta: {request.alias ?? "—"}\nTitular: {request.holder ?? "—"}\nWhatsApp: {request.whatsapp ?? "—"}", 27, TrucoClubUI.Cream, new Vector2(0, .48f), new Vector2(1, sale ? .69f : .75f));
        TrucoClubUI.Button(_body, "COPIAR ALIAS", () => { if (!string.IsNullOrEmpty(request.alias)) { GUIUtility.systemCopyBuffer = request.alias; _notice.text = "Alias copiado"; } }, new Vector2(0, .38f), new Vector2(1, .47f), true);
        var receipt = TrucoClubUI.Button(_body, "ENVIAR COMPROBANTE", () =>
        {
            if (Uri.TryCreate(request.receiptUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https") Application.OpenURL(uri.AbsoluteUri);
        }, new Vector2(0, .28f), new Vector2(1, .37f));
        receipt.interactable = request.type == "buy" && Uri.TryCreate(request.receiptUrl, UriKind.Absolute, out var link) && link.Scheme == "https";
        string stages = ""; if (request.history != null) foreach (var stage in request.history) if (stage != null) stages += TrucoWalletService.StatusLabel(stage.status) + " · " + stage.at + "\n";
        TrucoClubUI.Text(_body, stages, 22, TrucoClubUI.Muted, new Vector2(0, .1f), new Vector2(1, .27f));
        TrucoClubUI.Button(_body, "CONSULTAR ESTADO", async () =>
        {
            if (_busy) return; _busy = true; int generation = _generation;
            TrucoWalletService.Request confirmedStatus = null;
            _notice.text = "Consultando la solicitud y el saldo…";
            try
            {
                confirmedStatus = await TrucoWalletService.Status(request.id);
                if (this == null || generation != _generation) return;
                var snapshot = await TrucoWalletService.Refresh();
                if (this == null || generation != _generation) return;
                _snapshot = snapshot;
                bool profileUpdated = await ApiController.GetCurrentUserProfile();
                if (this == null || generation != _generation) return;
                Details(confirmedStatus);
                if (!profileUpdated) _notice.text = "Estado actualizado. No se pudo actualizar el saldo del perfil. Reintentá la consulta.";
            }
            catch (Exception ex)
            {
                if (this != null && generation == _generation)
                {
                    if (confirmedStatus != null) Details(confirmedStatus);
                    _notice.text = "No se pudo completar la actualización del saldo. " + ex.Message;
                }
            }
            finally { if (this != null) _busy = false; }
        }, Vector2.zero, new Vector2(1, .09f), true);
    }
}
