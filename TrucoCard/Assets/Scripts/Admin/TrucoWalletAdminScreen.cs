using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Real server-backed wallet review. No sample records or optimistic payment states.</summary>
public sealed class TrucoWalletAdminScreen : MonoBehaviour
{
    static TrucoWalletAdminScreen _instance;
    RectTransform _safe, _body;
    CanvasGroup _bodyGroup;
    TextMeshProUGUI _title, _notice;
    Button _refresh;
    readonly List<TrucoWalletAdminService.Record> _rows = new List<TrucoWalletAdminService.Record>();
    TrucoWalletAdminService.Record _record, _pendingRecord;
    TrucoWalletAdminService.Command _pendingCommand;
    string _pendingAction, _cursor, _filter;
    int _generation;
    bool _busy, _detail;

#if UNITY_EDITOR
    public static GameObject EditorPreview(TrucoWalletAdminService.Record[] records, TrucoWalletAdminService.Record detail = null)
    {
        var root = new GameObject("WalletAdminEditorPreview");
        var screen = root.AddComponent<TrucoWalletAdminScreen>(); screen.Build();
        if (detail != null) screen.RenderDetail(detail);
        else { if (records != null) screen._rows.AddRange(records); screen.RenderList(); }
        screen._notice.text = "VISTA EDITOR · Datos de ejemplo · Sin operaciones reales";
        return root;
    }
#endif

    public static void Show()
    {
        if (_instance == null)
        {
            var root = new GameObject("TrucoinsAdminReview");
            _instance = root.AddComponent<TrucoWalletAdminScreen>(); _instance.Build();
        }
        _instance.gameObject.SetActive(true); _ = _instance.LoadList(false);
    }
    void Build()
    {
        _safe = TrucoClubUI.Screen(gameObject, TrucoCanvasOrder.Screen);
        _title = TrucoClubUI.Text(_safe, "GESTIÓN DE TRUCOINS", 42, TrucoClubUI.Gold, new Vector2(.06f, .90f), new Vector2(.94f, .99f), true);
        _notice = TrucoClubUI.Text(_safe, "", 25, TrucoClubUI.Cream, new Vector2(.06f, .79f), new Vector2(.94f, .90f));
        TrucoClubUI.Button(_safe, "VOLVER", () => {
            if (_detail) { _ = LoadList(false); return; }
            _generation++; Busy(false); gameObject.SetActive(false);
        }, new Vector2(.06f, .02f), new Vector2(.48f, .11f), true);
        _refresh = TrucoClubUI.Button(_safe, "ACTUALIZAR", () => {
            if (_detail && _record != null) _ = LoadDetail(_record.request.id);
            else _ = LoadList(false);
        }, new Vector2(.52f, .02f), new Vector2(.94f, .11f), true);
    }
    void Page(string title)
    {
        _generation++; _title.text = title;
        if (_body != null) TrucoMenuTheme.DestroyUI(_body.gameObject);
        _body = TrucoClubUI.Rect("Content", _safe, new Vector2(.06f, .14f), new Vector2(.94f, .79f));
        _bodyGroup = _body.gameObject.AddComponent<CanvasGroup>();
    }
    void Busy(bool value)
    {
        _busy = value;
        if (_bodyGroup != null) _bodyGroup.interactable = !value;
        if (_refresh != null) _refresh.interactable = !value;
    }
    async Task LoadList(bool append)
    {
        if (_busy) return;
        _detail = false;
        if (!append) { Page("GESTIÓN DE TRUCOINS"); _rows.Clear(); _cursor = null; }
        Busy(true); int generation = _generation; _notice.text = "Verificando permisos y consultando solicitudes…";
        try
        {
            var page = await TrucoWalletAdminService.List(append ? _cursor : null, _filter);
            if (this == null || generation != _generation) return;
            foreach (var row in page.items)
            {
                int index = _rows.FindIndex(existing => existing.request.id == row.request.id);
                if (index < 0) _rows.Add(row); else if (row.revision >= _rows[index].revision) _rows[index] = row;
            }
            _cursor = page.nextCursor; Busy(false); RenderList();
            _notice.text = _rows.Count == 0 ? "No hay solicitudes en este filtro." : "Solicitudes confirmadas por el servidor.";
        }
        catch (Exception exception) { if (this != null && generation == _generation) _notice.text = exception.Message; }
        finally { if (this != null && generation == _generation) Busy(false); }
    }
    void RenderList()
    {
        Page("GESTIÓN DE TRUCOINS");
        string[] labels = { "TODAS", "COMPRAS", "VENTAS" }; string[] filters = { null, "buy", "sell" };
        for (int i = 0; i < labels.Length; i++)
        {
            string filter = filters[i];
            TrucoClubUI.Button(_body, labels[i], () => { _filter = filter; _ = LoadList(false); },
                new Vector2(i / 3f, .90f), new Vector2((i + 1) / 3f, 1), _filter != filter);
        }
        var scroll = CreateScroll(_body, "Requests", new Vector2(0, .12f), new Vector2(1, .89f), out var content);
        content.sizeDelta = new Vector2(0, _rows.Count * 240);
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i]; var request = row.request;
            var card = TrucoClubUI.Panel("Request_" + request.id, content, TrucoClubUI.Surface, new Vector2(0, 1), new Vector2(1, 1));
            var rect = card.rectTransform; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-16, 224); rect.anchoredPosition = new Vector2(0, -i * 240);
            string operation = request.type == "buy" ? "COMPRA" : "VENTA";
            TrucoClubUI.Text(card.transform, operation + " · " + request.amount + " TRUCOINS", 32, TrucoClubUI.Gold, new Vector2(.02f, .64f), new Vector2(.98f, .98f), true);
            TrucoClubUI.Text(card.transform, (row.owner.username ?? row.owner.id) + "\n" + TrucoWalletService.StatusLabel(request.status) + " · " + request.createdAt,
                26, TrucoClubUI.Cream, new Vector2(.02f, .17f), new Vector2(.98f, .66f));
            TrucoClubUI.Button(card.transform, "REVISAR", () => _ = LoadDetail(request.id), new Vector2(.64f, .02f), new Vector2(.98f, .31f));
        }
        var more = TrucoClubUI.Button(_body, "CARGAR MÁS", () => _ = LoadList(true), Vector2.zero, new Vector2(1, .10f), true);
        more.interactable = !string.IsNullOrEmpty(_cursor);
        _ = scroll;
    }
    async Task LoadDetail(string id)
    {
        if (_busy) return;
        Busy(true); int generation = _generation; _notice.text = "Consultando la solicitud…";
        try
        {
            string key = _pendingRecord?.request.id == id ? _pendingCommand?.idempotencyKey : null;
            var record = await TrucoWalletAdminService.Detail(id, key);
            if (this == null || generation != _generation) return;
            if (_pendingRecord?.request.id == id && TrucoWalletAdminService.MatchesOperation(record.operationReceipt, id, _pendingAction, _pendingCommand)
                && record.revision >= record.operationReceipt.revision) ClearPending();
            Busy(false); RenderDetail(record);
            _notice.text = _pendingCommand == null ? "Estado confirmado por el servidor." : "Hay una acción sin confirmar. Consultá su estado o reintentá la misma operación.";
        }
        catch (Exception exception) { if (this != null && generation == _generation) _notice.text = exception.Message; }
        finally { if (this != null && generation == _generation) Busy(false); }
    }
    void RenderDetail(TrucoWalletAdminService.Record record)
    {
        _record = record; _detail = true; Page("REVISAR SOLICITUD");
        var request = record.request;
        var info = TrucoClubUI.Panel("RequestSummary", _body, TrucoClubUI.Surface, new Vector2(0, .62f), Vector2.one);
        TrucoClubUI.Text(info.transform, request.amount + " TRUCOINS · " + TrucoWalletService.StatusLabel(request.status), 37, TrucoClubUI.Gold, new Vector2(.02f, .73f), new Vector2(.98f, .99f), true);
        TrucoClubUI.Text(info.transform, (request.type == "buy" ? "COMPRA" : "VENTA") + " · " + (record.owner.username ?? record.owner.id) + "\n" + request.id + " · Revisión " + record.revision,
            26, TrucoClubUI.Cream, new Vector2(.02f, .48f), new Vector2(.98f, .74f));
        TrucoClubUI.Text(info.transform, $"Banco: {request.bank ?? "—"}\nCuenta: {request.alias ?? "—"}\nTitular: {request.holder ?? "—"}\nContacto: {request.whatsapp ?? "—"}", 25,
            TrucoClubUI.Cream, new Vector2(.02f, .01f), new Vector2(.98f, .49f));
        var receipt = TrucoClubUI.Button(_body, "VER COMPROBANTE", () => {
            if (Uri.TryCreate(record.receiptDownloadUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https") Application.OpenURL(uri.AbsoluteUri);
        }, new Vector2(0, .52f), new Vector2(1, .61f), true);
        receipt.interactable = Uri.TryCreate(record.receiptDownloadUrl, UriKind.Absolute, out var receiptUri) && receiptUri.Scheme == "https";
        CreateScroll(_body, "Audit", new Vector2(0, .32f), new Vector2(1, .51f), out var auditContent);
        var audit = record.audit ?? new List<TrucoWalletAdminService.AuditEntry>();
        auditContent.sizeDelta = new Vector2(0, Math.Max(1, audit.Count) * 92);
        for (int i = 0; i < Math.Max(1, audit.Count); i++)
        {
            string text = audit.Count == 0 ? "Sin eventos de auditoría recibidos." : audit[i].at + " · " + ActionCaption(audit[i].action) + "\n" + audit[i].reason;
            var label = TrucoClubUI.Text(auditContent, text, 24, TrucoClubUI.Muted, new Vector2(0, 1), new Vector2(1, 1));
            label.rectTransform.pivot = new Vector2(.5f, 1); label.rectTransform.sizeDelta = new Vector2(-20, 86); label.rectTransform.anchoredPosition = new Vector2(0, -i * 92);
        }
        var reason = TrucoClubUI.Input(_body, "Motivo de la decisión (obligatorio)", new Vector2(0, .21f), new Vector2(1, .30f)); reason.characterLimit = 500;
        var payment = TrucoClubUI.Input(_body, "Referencia de transferencia (para pagar)", new Vector2(0, .11f), new Vector2(1, .20f)); payment.characterLimit = 200;
        if (_pendingCommand != null)
        {
            reason.interactable = payment.interactable = false;
            reason.text = _pendingCommand.reason; payment.text = _pendingCommand.paymentReference;
            var retry = TrucoClubUI.Button(_body, "REINTENTAR " + ActionCaption(_pendingAction), ConfirmPending, Vector2.zero, new Vector2(1, .10f));
            retry.interactable = _pendingRecord?.request.id == request.id;
            return;
        }
        string[] actions = { "approve", "reject", "pay" };
        for (int i = 0; i < actions.Length; i++)
        {
            string action = actions[i];
            var button = TrucoClubUI.Button(_body, ActionCaption(action), () => {
                var command = new TrucoWalletAdminService.Command { expectedRevision = record.revision, reason = reason.text.Trim(), paymentReference = payment.text.Trim(), idempotencyKey = Guid.NewGuid().ToString("N") };
                string error = TrucoWalletAdminService.ValidateCommand(record, action, command);
                if (error != null) { _notice.text = error; return; }
                TrucoConfirmDialog.Show("CONFIRMAR " + ActionCaption(action),
                    record.request.amount + " Trucoins · " + record.request.id + "\n" + command.reason,
                    "VOLVER", "CONFIRMAR", () => {
                        _pendingRecord = record; _pendingAction = action; _pendingCommand = command; _ = SubmitPending();
                    });
            }, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, .10f), action == "reject");
            button.interactable = record.allowedActions != null && record.allowedActions.Contains(action);
        }
    }
    void ConfirmPending()
    {
        if (_busy || _pendingCommand == null || _pendingRecord == null) return;
        TrucoConfirmDialog.Show("CONFIRMAR " + ActionCaption(_pendingAction),
            _pendingRecord.request.amount + " Trucoins · " + _pendingRecord.request.id + "\n" + _pendingCommand.reason,
            "VOLVER", "CONFIRMAR", () => _ = SubmitPending());
    }
    async Task SubmitPending()
    {
        if (_busy || _pendingCommand == null) return;
        Busy(true); int generation = _generation; _notice.text = "Esperando confirmación del servidor…";
        try
        {
            var result = await TrucoWalletAdminService.Submit(_pendingRecord, _pendingAction, _pendingCommand);
            if (this == null || generation != _generation) return;
            string operationId = result.operation.id; ClearPending(); Busy(false); RenderDetail(result.record);
            _notice.text = "Operación confirmada por el servidor: " + operationId;
        }
        catch (Exception exception)
        {
            if (this != null && generation == _generation)
            {
                Busy(false); RenderDetail(_record);
                _notice.text = "Resultado sin confirmar. Actualizá para conciliar o reintentá la misma operación. " + exception.Message;
            }
        }
        finally { if (this != null && generation == _generation) Busy(false); }
    }
    void ClearPending() { _pendingCommand = null; _pendingRecord = null; _pendingAction = null; }
    static string ActionCaption(string action) => action == "approve" ? "APROBAR" : action == "reject" ? "RECHAZAR" : action == "pay" ? "PAGAR" : action ?? "";
    static ScrollRect CreateScroll(Transform parent, string name, Vector2 min, Vector2 max, out RectTransform content)
    {
        var root = TrucoClubUI.Rect(name, parent, min, max); var scroll = root.gameObject.AddComponent<ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", root, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        content = TrucoClubUI.Rect("Rows", viewport, new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(.5f, 1);
        scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; return scroll;
    }
}
