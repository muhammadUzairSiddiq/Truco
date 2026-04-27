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

    [Header("Truco look (Truco Assets / Top Panel)")]
    [SerializeField] private Sprite _woodTopPanelSprite;
    [SerializeField] private string _headerTitle = "LISTA DE TORNEOS: ABIERTOS";

    /// <summary>One horizontal bar: brown background, white title, back on the left. No double strips.</summary>
    const string HeaderRootName = "TournamentListHeader";

    [SerializeField] private float _topBarHeight = 108f;

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
                rootBg.sprite = null;
                rootBg.color = TrucoUiTheme.TournamentListScreenBg;
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

        var header = new GameObject(HeaderRootName, typeof(RectTransform));
        var headerRt = header.GetComponent<RectTransform>();
        header.transform.SetParent(holder, false);
        headerRt.SetAsFirstSibling();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(0f, _topBarHeight);
        headerRt.anchoredPosition = Vector2.zero;

        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        var barRt = bar.GetComponent<RectTransform>();
        bar.transform.SetParent(header.transform, false);
        barRt.anchorMin = Vector2.zero;
        barRt.anchorMax = Vector2.one;
        barRt.offsetMin = Vector2.zero;
        barRt.offsetMax = Vector2.zero;
        var barImg = bar.GetComponent<Image>();
        if (_woodTopPanelSprite != null)
        {
            barImg.sprite = _woodTopPanelSprite;
            barImg.type = Image.Type.Sliced;
        }
        barImg.color = TrucoUiTheme.TournamentTopBarSolid;

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

        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(header.transform, false);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(100f, 0f);
        trt.offsetMax = new Vector2(-16f, 0f);
        var tmp = title.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(_headerTitle) ? string.Empty : _headerTitle.Trim().ToUpperInvariant();
        tmp.fontSize = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20;
        tmp.fontSizeMax = 34;
        tmp.characterSpacing = 0.5f;

        var scroll = holder.Find("Scroll View") as RectTransform;
        if (scroll != null)
        {
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.pivot = new Vector2(0.5f, 0.5f);
            scroll.offsetMin = new Vector2(0f, 0f);
            scroll.offsetMax = new Vector2(0f, -_topBarHeight);
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
