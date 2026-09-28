using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Per-tournament admin: force-close, set champion, credit prize. Built at runtime from Manage.</summary>
public class ManageTournamentOverlay : MonoBehaviour
{
    static ManageTournamentOverlay _instance;
    GameObject _root;
    Tournament _data;
    TextMeshProUGUI _title;
    TMP_InputField _championId;
    TMP_InputField _prizeUser;
    TMP_InputField _prizeAmt;
    TMP_InputField _prizeReason;

    public static void Open(Tournament tournament)
    {
        Ensure().Show(tournament);
    }

    static ManageTournamentOverlay Ensure()
    {
        if (_instance != null) return _instance;
        var canvas = Object.FindObjectOfType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : null;
        var host = new GameObject("ManageTournamentOverlayHost", typeof(RectTransform), typeof(ManageTournamentOverlay));
        if (parent != null) host.transform.SetParent(parent, false);
        var rt = host.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _instance = host.GetComponent<ManageTournamentOverlay>();
        _instance.Build();
        return _instance;
    }

    void Build()
    {
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _root = new GameObject("ManageModal");
        _root.transform.SetParent(transform, false);
        var rr = _root.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;
        _root.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        var panel = new GameObject("P", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(720, 820);
        panel.GetComponent<Image>().color = new Color(0.11f, 0.09f, 0.08f, 0.98f);
        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(22, 22, 18, 18);
        v.spacing = 10;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        _title = MkText(panel.transform, "Gestionar torneo", 24, 48);
        MkBtn(panel.transform, "Cerrar", new Color(0.35f, 0.35f, 0.35f), Hide);
        MkBtn(panel.transform, "Forzar cierre del torneo", new Color(0.65f, 0.22f, 0.18f), ConfirmClose);
        _championId = MkInput(panel.transform, "userId del campeón");
        MkBtn(panel.transform, "Fijar campeón", new Color(0.35f, 0.45f, 0.22f), ConfirmChampion);
        _prizeUser = MkInput(panel.transform, "userId premio");
        _prizeAmt = MkInput(panel.transform, "Trucoins");
        _prizeReason = MkInput(panel.transform, "motivo");
        MkBtn(panel.transform, "Acreditar premio excepcional", new Color(0.55f, 0.42f, 0.12f), ConfirmPrize);
        _root.SetActive(false);
    }

    void Show(Tournament t)
    {
        _data = t;
        if (_title != null)
        {
            string name = t != null ? t.name : "";
            string id = t != null ? t._id : "";
            _title.text = "Gestionar\n" + name + "\n<size=18>" + id + "</size>";
        }
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
    }

    void Hide() => _root.SetActive(false);

    void ConfirmClose()
    {
        if (_data == null) return;
        TrucoConfirmDialog.Show(
            "Forzar cierre",
            "¿Cerrar el torneo " + _data.name + "?",
            "NO", "CERRAR",
            async () =>
            {
                bool ok = await ApiController.AdminForceCloseTournament(_data._id);
                TrucoAdminAuditLog.Record("FORCE_CLOSE_TOURNAMENT", _data._id + " ok=" + ok);
                AppManager.Instance?.DisplayNotification(ok ? "Torneo cerrado." : "No se pudo cerrar el torneo.");
            });
    }

    void ConfirmChampion()
    {
        if (_data == null) return;
        string uid = _championId != null ? _championId.text.Trim() : "";
        if (string.IsNullOrEmpty(uid))
        {
            AppManager.Instance?.DisplayNotification("Ingresá el userId del campeón.");
            return;
        }
        TrucoConfirmDialog.Show(
            "Campeón excepcional",
            "¿Fijar a " + uid + " como campeón?",
            "NO", "SÍ",
            async () =>
            {
                bool ok = await ApiController.FinalizeTournament(_data._id, uid);
                TrucoAdminAuditLog.Record("MANUAL_CHAMPION", _data._id + " → " + uid + " ok=" + ok);
                AppManager.Instance?.DisplayNotification(ok ? "Campeón fijado." : "No se pudo fijar el campeón.");
            });
    }

    void ConfirmPrize()
    {
        string uid = _prizeUser != null ? _prizeUser.text.Trim() : "";
        if (string.IsNullOrEmpty(uid) || _prizeAmt == null || !int.TryParse(_prizeAmt.text.Trim(), out int amt) || amt <= 0)
        {
            AppManager.Instance?.DisplayNotification("UserId y cantidad válidos.");
            return;
        }
        string reason = _prizeReason != null ? _prizeReason.text.Trim() : ("premio torneo " + (_data != null ? _data._id : ""));
        TrucoConfirmDialog.Show(
            "Premio excepcional",
            "¿Acreditar " + amt + " Trucoins?",
            "NO", "ACREDITAR",
            async () =>
            {
                bool ok = await ApiController.AdminCreditUserCoins(uid, amt, reason);
                TrucoAdminAuditLog.Record("MANUAL_PRIZE", uid + " +" + amt + " ok=" + ok);
                AppManager.Instance?.DisplayNotification(ok ? "Premio acreditado." : "No se pudo acreditar.");
            });
    }

    static TextMeshProUGUI MkText(Transform parent, string text, float size, float h)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = h;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.richText = true;
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        return t;
    }

    static void MkBtn(Transform parent, string label, Color bg, UnityEngine.Events.UnityAction click)
    {
        var go = new GameObject("B", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = 48;
        go.GetComponent<Image>().color = bg;
        var child = new GameObject("L");
        child.transform.SetParent(go.transform, false);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        var lrt = child.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        go.GetComponent<Button>().onClick.AddListener(click);
    }

    static TMP_InputField MkInput(Transform parent, string placeholder)
    {
        var go = new GameObject("In", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = 44;
        go.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.14f);
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 4); trt.offsetMax = new Vector2(-10, -4);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 18; tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        var phGo = new GameObject("Ph", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(go.transform, false);
        var prt = phGo.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(10, 4); prt.offsetMax = new Vector2(-10, -4);
        var ph = phGo.GetComponent<TextMeshProUGUI>();
        ph.text = placeholder; ph.fontSize = 18; ph.color = new Color(1, 1, 1, 0.35f);
        if (TMP_Settings.defaultFontAsset != null) ph.font = TMP_Settings.defaultFontAsset;
        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = go.GetComponent<RectTransform>();
        input.textComponent = tmp;
        input.placeholder = ph;
        return input;
    }
}
