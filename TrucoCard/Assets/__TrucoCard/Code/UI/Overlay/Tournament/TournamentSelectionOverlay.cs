using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TournamentSelectionOverlay : MonoBehaviour
{
    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private RectTransform _tournamentsCardHolder;
    [SerializeField] private TournamentCardItem _tournamentCardPF;
    [Tooltip("Legacy; always hidden at runtime.")]
    [SerializeField] private Button _closeTournamentSelectionUiBtn;

    [Header("Truco look")]
    [Tooltip("Optional slice sprite for subtle texture; if null, solid brown only.")]
    [SerializeField] private Sprite _woodTopPanelSprite;
    [Tooltip("Encabezado en español (MAYÚSCULAS recomendado).")]
    [SerializeField] private string _headerTitle = "TORNEOS";

    /// <summary>Brown block + back + título (sin barra anidada duplicada).</summary>
    const string HeaderRootName = "TournamentListHeader";

    [SerializeField] private float _headerBlockHeight = 128f;

    private List<TournamentCardItem> _instantiatedCards = new List<TournamentCardItem>();

    private void Awake()
    {
        if (_closeTournamentSelectionUiBtn != null)
            _closeTournamentSelectionUiBtn.gameObject.SetActive(false);

        if (_contentHolder != null)
        {
            MigrateRemoveLegacyHeader();
            EnsureListChrome();
            if (_contentHolder.GetComponent<Graphic>() is Image rootBg)
            {
                rootBg.enabled = true;
                if (TrucoGameplayUiSprites.PanelBrown != null)
                {
                    rootBg.sprite = TrucoGameplayUiSprites.PanelBrown;
                    rootBg.type = Image.Type.Sliced;
                    rootBg.color = Color.white;
                }
                else
                {
                    rootBg.sprite = null;
                    rootBg.color = new Color(0.12f, 0.07f, 0.04f, 1f);
                }
            }
        }
    }

    void MigrateRemoveLegacyHeader()
    {
        if (_contentHolder == null) return;
        var t = _contentHolder.transform;
        for (var i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (c == null) continue;
            if (c.name == "TournamentListHeaderRoot" || c.name == HeaderRootName)
                Destroy(c.gameObject);
        }
    }

    private void Start()
    {
        if (_closeTournamentSelectionUiBtn != null)
            _closeTournamentSelectionUiBtn.onClick.RemoveAllListeners();
    }

    void EnsureListChrome()
    {
        if (_contentHolder == null) return;
        var holder = _contentHolder.transform;
        if (holder.Find(HeaderRootName) != null) return;

        var rootRect = _contentHolder.GetComponent<RectTransform>();
        if (rootRect == null) return;

        // Un solo bloque marrón: el propio contenedor es el fondo; hijos = volver + título.
        var header = new GameObject(HeaderRootName, typeof(RectTransform), typeof(Image));
        var headerRt = header.GetComponent<RectTransform>();
        var headerBg = header.GetComponent<Image>();
        if (_woodTopPanelSprite != null)
        {
            headerBg.sprite = _woodTopPanelSprite;
            headerBg.type = Image.Type.Sliced;
        }
        else
        {
            headerBg.sprite = null;
        }
        headerBg.color = new Color(0.18f, 0.11f, 0.06f, 1f);
        header.transform.SetParent(holder, false);
        headerRt.SetAsFirstSibling();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(0f, _headerBlockHeight);
        headerRt.anchoredPosition = Vector2.zero;

        var back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        var backRt = back.GetComponent<RectTransform>();
        back.transform.SetParent(header.transform, false);
        backRt.anchorMin = new Vector2(0f, 0.5f);
        backRt.anchorMax = new Vector2(0f, 0.5f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(56f, 0f);
        backRt.sizeDelta = new Vector2(84f, 72f);
        var backImg = back.GetComponent<Image>();
        backImg.color = new Color(0, 0, 0, 0.2f);
        var backBtn = back.GetComponent<Button>();
        var ccb = backBtn.colors;
        ccb.fadeDuration = 0.1f;
        ccb.normalColor = Color.white;
        ccb.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        ccb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        backBtn.colors = ccb;
        backBtn.onClick.AddListener(() => MainMenuViewCoordinator.ReturnToMenuFromTournament());
        var backLabel = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        backLabel.transform.SetParent(back.transform, false);
        var btmp = backLabel.GetComponent<TextMeshProUGUI>();
        btmp.text = "←";
        btmp.fontSize = 44;
        btmp.fontStyle = FontStyles.Bold;
        btmp.color = Color.white;
        btmp.alignment = TextAlignmentOptions.Center;
        var blRt = backLabel.GetComponent<RectTransform>();
        blRt.anchorMin = Vector2.zero;
        blRt.anchorMax = Vector2.one;
        blRt.offsetMin = Vector2.zero;
        blRt.offsetMax = Vector2.zero;

        if (TrucoGameplayUiSprites.PanelBrown != null)
        {
            headerBg.sprite = TrucoGameplayUiSprites.PanelBrown;
            headerBg.type = Image.Type.Sliced;
            headerBg.color = Color.white;
        }
        if (TrucoGameplayUiSprites.Back != null)
        {
            backImg.sprite = TrucoGameplayUiSprites.Back;
            backImg.color = Color.white;
            backImg.preserveAspect = true;
            backLabel.SetActive(false);
        }

        var laurelL = new GameObject("LaurelL", typeof(RectTransform), typeof(Image));
        laurelL.transform.SetParent(header.transform, false);
        var ll = laurelL.GetComponent<RectTransform>();
        ll.anchorMin = ll.anchorMax = new Vector2(0.5f, 0.62f);
        ll.sizeDelta = new Vector2(72f, 64f);
        ll.anchoredPosition = new Vector2(-168f, 4f);
        var lli = laurelL.GetComponent<Image>();
        lli.preserveAspect = true;
        lli.raycastTarget = false;
        if (TrucoGameplayUiSprites.Laurel != null) { lli.sprite = TrucoGameplayUiSprites.Laurel; lli.color = Color.white; }

        var laurelR = new GameObject("LaurelR", typeof(RectTransform), typeof(Image));
        laurelR.transform.SetParent(header.transform, false);
        var lr = laurelR.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.62f);
        lr.sizeDelta = new Vector2(72f, 64f);
        lr.anchoredPosition = new Vector2(168f, 4f);
        lr.localScale = new Vector3(-1f, 1f, 1f);
        var lri = laurelR.GetComponent<Image>();
        lri.preserveAspect = true;
        lri.raycastTarget = false;
        if (TrucoGameplayUiSprites.Laurel != null) { lri.sprite = TrucoGameplayUiSprites.Laurel; lri.color = Color.white; }

        var title = new GameObject("Encabezado", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(header.transform, false);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.38f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(110f, 0f);
        trt.offsetMax = new Vector2(-16f, 0f);
        var tmp = title.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(_headerTitle) ? "TORNEOS" : _headerTitle.Trim().ToUpperInvariant();
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.86f, 0.38f, 1f);
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 24;
        tmp.fontSizeMax = 42;
        tmp.characterSpacing = 2.4f;

        var tab = new GameObject("Activos", typeof(RectTransform), typeof(TextMeshProUGUI));
        tab.transform.SetParent(header.transform, false);
        var tabRt = tab.GetComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0.5f, 0f);
        tabRt.anchorMax = new Vector2(0.5f, 0f);
        tabRt.pivot = new Vector2(0.5f, 0f);
        tabRt.anchoredPosition = new Vector2(0f, 8f);
        tabRt.sizeDelta = new Vector2(280f, 36f);
        var tabT = tab.GetComponent<TextMeshProUGUI>();
        tabT.text = "ACTIVOS";
        tabT.fontSize = 20f;
        tabT.fontStyle = FontStyles.Bold;
        tabT.color = new Color(0.95f, 0.82f, 0.42f, 1f);
        tabT.alignment = TextAlignmentOptions.Center;
        tabT.characterSpacing = 4f;

        var scroll = holder.Find("Scroll View") as RectTransform;
        if (scroll != null)
        {
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.pivot = new Vector2(0.5f, 0.5f);
            scroll.offsetMin = new Vector2(0f, 0f);
            scroll.offsetMax = new Vector2(0f, -_headerBlockHeight);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }

    public void DisplayTournamentSelectionUI(Tournament[] activeTournaments)
    {
        if (_contentHolder == null) return;
        if (_tournamentsCardHolder == null) return;
        EnsureListChrome();

        foreach (var t in activeTournaments)
        {
            var tournamentCard = Instantiate(_tournamentCardPF, _tournamentsCardHolder);
            tournamentCard.Init(t, _woodTopPanelSprite);
            _instantiatedCards.Add(tournamentCard);
        }

        _contentHolder.gameObject.SetActive(true);
        MainMenuViewCoordinator.EnsureTournamentNavPriority();

        LayoutRebuilder.ForceRebuildLayoutImmediate(_tournamentsCardHolder);
        Canvas.ForceUpdateCanvases();
    }

    public void HideTournamentSelectionUI()
    {
        if (_contentHolder != null) _contentHolder.SetActive(false);
        foreach (var t in _instantiatedCards)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _instantiatedCards.Clear();
    }
}
