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

    [SerializeField] private float _headerBlockHeight = 96f;

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
        headerBg.color = TrucoUiTheme.TournamentTopBarSolid;
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

        var title = new GameObject("Encabezado", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(header.transform, false);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(100f, 0f);
        trt.offsetMax = new Vector2(-16f, 0f);
        var tmp = title.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(_headerTitle) ? "TORNEOS" : _headerTitle.Trim().ToUpperInvariant();
        tmp.fontSize = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 22;
        tmp.fontSizeMax = 40;
        tmp.characterSpacing = 1.2f;

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
