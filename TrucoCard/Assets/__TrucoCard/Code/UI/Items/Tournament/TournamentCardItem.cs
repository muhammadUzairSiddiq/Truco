using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MH.Multiplayer;

public class TournamentCardItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _rulesText;
    [SerializeField] private Button _joinTournamentBtn;

    [Header("Read-Only")]
    [SerializeField] private Tournament _iTournamentData;

    TMP_InputField _passwordField;
    Transform _visualRoot;
    bool _built;

    public void Init(Tournament tournament, Sprite woodPanelSprite = null)
    {
        _iTournamentData = tournament;
        EnsureVisual();
        Bind();
        _ = woodPanelSprite;
    }

    void EnsureVisual()
    {
        if (_built) return;
        _built = true;

        HideLegacy("TitleBG");
        HideLegacy("DescriptionBG");
        HideLegacy("RulesBG");
        if (_titleText != null) _titleText.gameObject.SetActive(false);
        if (_descriptionText != null) _descriptionText.gameObject.SetActive(false);
        if (_rulesText != null) _rulesText.gameObject.SetActive(false);

        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.minWidth = 0f;
        le.flexibleWidth = 1f;
        le.minHeight = 394f;
        le.preferredHeight = 410f;

        var self = GetComponent<Image>();
        if (self == null) self = gameObject.AddComponent<Image>();
        TrucoMenuTheme.Surface(self, TrucoMenuTheme.Brown);
        self.raycastTarget = false;

        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 26, 26);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _visualRoot = transform;

        var header = Row("Header");
        header.GetComponent<LayoutElement>().minHeight = 58f;
        header.GetComponent<LayoutElement>().preferredHeight = 60f;
        _titleText = MakeTmp(header, "Name", 36f, FontStyles.Bold, TrucoMenuTheme.Gold);
        _titleText.alignment = TextAlignmentOptions.MidlineLeft;
        _titleText.enableAutoSizing = true;
        _titleText.fontSizeMin = 26f;
        _titleText.fontSizeMax = 36f;
        _titleText.overflowMode = TextOverflowModes.Ellipsis;
        _titleText.enableWordWrapping = false;

        var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        badge.transform.SetParent(header, false);
        var ble = badge.GetComponent<LayoutElement>();
        ble.minWidth = 172f;
        ble.preferredWidth = 192f;
        ble.flexibleWidth = 0f;
        ble.minHeight = 48f;
        var bImg = badge.GetComponent<Image>();
        TrucoMenuTheme.Surface(bImg, TrucoMenuTheme.DeepBrown);
        bImg.raycastTarget = false;
        var bh = badge.GetComponent<HorizontalLayoutGroup>();
        bh.padding = new RectOffset(10, 12, 4, 4);
        bh.spacing = 6f;
        bh.childAlignment = TextAnchor.MiddleCenter;
        bh.childControlWidth = true;
        bh.childControlHeight = true;
        Icon(badge.transform, "LockI", TrucoGameplayUiSprites.IconLock, 32f);
        MakeTmp(badge.transform, "BadgeT", 23f, FontStyles.Bold, TrucoMenuTheme.Gold).alignment = TextAlignmentOptions.Center;

        IconRow("Meta1");
        IconRow("Meta2");
        IconRow("Meta3");

        var passRow = Row("PasswordRow");
        Icon(passRow, "Lock", TrucoGameplayUiSprites.IconLock, 36f);
        _passwordField = MakeInput(passRow, "Ingresar contraseña");
        passRow.GetComponent<LayoutElement>().minHeight = 82f;
        passRow.GetComponent<LayoutElement>().preferredHeight = 82f;
        var enterMini = MakeButton(passRow, "EnterMini", "ENTRAR", 190f, 82f, false);
        enterMini.onClick.AddListener(JoinTournamentBtn_fn);

        if (_joinTournamentBtn == null)
            _joinTournamentBtn = MakeButton(transform, "Join", "INSCRIBIRSE", 0f, 82f, true);
        else
        {
            _joinTournamentBtn.transform.SetParent(transform, false);
            _joinTournamentBtn.transform.SetAsLastSibling();
            var jle = _joinTournamentBtn.GetComponent<LayoutElement>();
            if (jle == null) jle = _joinTournamentBtn.gameObject.AddComponent<LayoutElement>();
            jle.minHeight = 82f;
            jle.preferredHeight = 82f;
            _joinTournamentBtn.transform.localScale = Vector3.one;
            TrucoMenuTheme.StyleButton(_joinTournamentBtn);
            var cap = _joinTournamentBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (cap != null)
            {
                cap.text = "INSCRIBIRSE";
                cap.fontStyle = FontStyles.Bold;
                cap.color = Color.white;
                cap.fontSize = 30f;
            }
        }
    }

    void Bind()
    {
        if (_iTournamentData == null) return;
        bool priv = _iTournamentData.IsPrivateTournament();
        var background = GetComponent<Image>();
        if (background != null) TrucoMenuTheme.Surface(background, TrucoMenuTheme.Brown);
        bool registered = _iTournamentData.IsCurrentUserRegistered();
        int n = _iTournamentData.participants != null ? _iTournamentData.participants.Count : 0;
        int max = Mathf.Max(1, _iTournamentData.maxPlayers);
        int score = TrucoMatchRules.NormalizeTargetScore(_iTournamentData.targetScore);
        string flor = _iTournamentData.withFlor ? "Con Flor" : "Sin Flor";

        if (_titleText != null) _titleText.text = _iTournamentData.name ?? "";

        var badgeT = transform.Find("Header/Badge/BadgeT");
        var badgeI = transform.Find("Header/Badge/LockI");
        if (badgeT != null)
        {
            var tmp = badgeT.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = priv ? "PRIVADO" : "PÚBLICO";
        }
        if (badgeI != null) badgeI.gameObject.SetActive(priv);

        SetIconLine("Meta1", TrucoGameplayUiSprites.IconPlayers, max + " Jugadores",
            _iTournamentData.withFlor ? TrucoGameplayUiSprites.IconFlor : TrucoGameplayUiSprites.IconCards,
            flor,
            TrucoGameplayUiSprites.IconCards, score + " Puntos");
        SetIconLine("Meta2", TrucoGameplayUiSprites.IconCoins, "Ingreso  " + _iTournamentData.entryFee + " Trucoins",
            TrucoGameplayUiSprites.IconTrophy, "Premio  " + _iTournamentData.prizePool + " Trucoins",
            null, null);
        SetIconLine("Meta3", TrucoGameplayUiSprites.IconGroup, "Jugadores inscritos  " + n + " / " + max,
            null, null, null, null);

        var passRow = transform.Find("PasswordRow");
        if (passRow != null) passRow.gameObject.SetActive(priv && !registered);
        if (_joinTournamentBtn != null)
        {
            _joinTournamentBtn.gameObject.SetActive(!priv || registered);
            var cap = _joinTournamentBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (cap != null) cap.text = registered ? "ENTRAR" : "INSCRIBIRSE";
            _joinTournamentBtn.onClick.RemoveAllListeners();
            _joinTournamentBtn.onClick.AddListener(JoinTournamentBtn_fn);
        }
    }

    void SetIconLine(string rowName, Sprite a, string at, Sprite b, string bt, Sprite c, string ct)
    {
        var row = transform.Find(rowName);
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--) TrucoMenuTheme.DestroyUI(row.GetChild(i).gameObject);
        if (a != null) Icon(row, "A", a, 34f);
        if (!string.IsNullOrEmpty(at)) Chip(row, at);
        if (b != null) Icon(row, "B", b, 34f);
        if (!string.IsNullOrEmpty(bt)) Chip(row, bt);
        if (c != null) Icon(row, "C", c, 34f);
        if (!string.IsNullOrEmpty(ct)) Chip(row, ct);
    }

    void Chip(Transform parent, string text)
    {
        var t = MakeTmp(parent, "T", 25f, FontStyles.Normal, TrucoClubUI.Cream);
        t.text = text ?? "";
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 80f;
    }

    Transform Row(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(_visualRoot, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        go.GetComponent<LayoutElement>().minHeight = 48f;
        go.GetComponent<LayoutElement>().preferredHeight = 48f;
        return go.transform;
    }

    Transform IconRow(string name) => Row(name);

    static void Icon(Transform parent, string name, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = le.minHeight = size;
        le.preferredWidth = le.preferredHeight = size;
        le.flexibleWidth = 0f;
        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
        else img.color = new Color(0.95f, 0.8f, 0.35f, 1f);
    }

    static TextMeshProUGUI MakeTmp(Transform parent, string name, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.7f;
        tmp.fontSizeMax = size;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        return tmp;
    }

    static TMP_InputField MakeInput(Transform parent, string placeholder)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        go.GetComponent<LayoutElement>().minHeight = 82f;
        var img = go.GetComponent<Image>();
        TrucoMenuTheme.Surface(img, TrucoMenuTheme.Paper);
        var input = go.GetComponent<TMP_InputField>();
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10, 4); tr.offsetMax = new Vector2(-10, -4);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 27f;
        tmp.color = TrucoMenuTheme.Ink;
        var phGo = new GameObject("Ph", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(go.transform, false);
        var pr = phGo.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = new Vector2(10, 4); pr.offsetMax = new Vector2(-10, -4);
        var pht = phGo.GetComponent<TextMeshProUGUI>();
        pht.text = placeholder;
        pht.fontSize = 25f;
        pht.fontStyle = FontStyles.Normal;
        pht.color = TrucoMenuTheme.Secondary;
        input.textViewport = tr;
        input.textComponent = tmp;
        input.placeholder = pht;
        input.characterLimit = 32;
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            pht.font = TMP_Settings.defaultFontAsset;
        }
        return input;
    }

    static Button MakeButton(Transform parent, string name, string label, float width, float height, bool flex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        if (flex) le.flexibleWidth = 1f;
        else { le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f; }
        var img = go.GetComponent<Image>();
        TrucoMenuTheme.Surface(img, TrucoClubUI.Green);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cap = MakeTmp(go.transform, "Cap", 24f, FontStyles.Bold, Color.white);
        cap.text = label;
        cap.alignment = TextAlignmentOptions.Center;
        cap.raycastTarget = false;
        var crt = cap.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        TrucoMenuTheme.StyleButton(btn);
        return btn;
    }

    void HideLegacy(string child)
    {
        var t = transform.Find(child);
        if (t != null) t.gameObject.SetActive(false);
    }

    private void JoinTournamentBtn_fn()
    {
        if (_iTournamentData == null) return;
        if (!TrucoPlayAccess.TryTournaments()) return;
        if (ApiController.GetSessionUser.Data.wallet.balance < _iTournamentData.entryFee
            && !_iTournamentData.IsCurrentUserRegistered())
        {
            AppManager.Instance.DisplayNotification("No tenés Trucoins suficientes para inscribirte.");
            return;
        }

        if (_iTournamentData.IsPrivateTournament() && !_iTournamentData.IsCurrentUserRegistered())
        {
            string pw = _passwordField != null ? _passwordField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(pw))
            {
                TournamentManager.Instance.DisplayTournamentPasswordInputOverlay(_iTournamentData._id, ConfirmRegistration);
                return;
            }
            ValidateThenJoin(pw);
            return;
        }

        ConfirmRegistration();
    }

    async void ValidateThenJoin(string password)
    {
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.ValidandoContrasena);
        bool ok = false;
        try { ok = await ApiController.ValidatePrivateTournament(_iTournamentData._id, password); }
        catch (System.Exception) { }
        AppManager.Instance.HideLoadingUI();
        if (ok) ConfirmRegistration();
        else AppManager.Instance.DisplayNotification(TrucoTextosClient.ContrasenaIncorrecta);
    }

    void ConfirmRegistration()
    {
        if (_iTournamentData.IsCurrentUserRegistered()) { DoMatchMaking(); return; }
        TrucoConfirmDialog.Show("INSCRIBIRSE AL TORNEO", _iTournamentData.name + "\n"
            + _iTournamentData.maxPlayers + " jugadores · " + _iTournamentData.targetScore + " puntos\n"
            + (_iTournamentData.withFlor ? "Con Flor" : "Sin Flor") + "\nEntrada: " + _iTournamentData.entryFee
            + " Trucoins · Premio previsto: " + _iTournamentData.prizePool + " Trucoins",
            "VOLVER", "INSCRIBIRME", DoMatchMaking);
    }
    private void DoMatchMaking()
    {
        TournamentRequest request = new TournamentRequest
        (
            _iTournamentData._id,
            _iTournamentData.maxPlayers,
            _iTournamentData.entryFee,
            _iTournamentData.name,
            _iTournamentData.prizePool,
            _iTournamentData.withFlor,
            _iTournamentData.targetScore,
            _iTournamentData.IsPrivateTournament()
        );
        TournamentManager.Instance.DisplayTournamentMatchmakingUI(request);
    }
}

public static class TournamentClientExtensions
{
    public static bool IsPrivateTournament(this Tournament t)
    {
        if (t == null) return false;
        if (t.passwordRequired) return true;
        var ty = (t.type ?? "").ToLowerInvariant();
        return ty == "private" || ty == "privado";
    }

    public static bool IsCurrentUserRegistered(this Tournament t)
    {
        var uid = ApiController.GetSessionUser?.Data?._id;
        if (t == null || string.IsNullOrEmpty(uid) || t.participants == null) return false;
        for (int i = 0; i < t.participants.Count; i++)
        {
            var p = t.participants[i];
            if (p == null) continue;
            if (p._id == uid || p.id == uid) return true;
        }
        return false;
    }
}
