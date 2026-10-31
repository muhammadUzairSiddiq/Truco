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
        _headerBlockHeight = 360f;
        if (_closeTournamentSelectionUiBtn != null)
            _closeTournamentSelectionUiBtn.gameObject.SetActive(false);

        if (_contentHolder != null)
        {
            MigrateRemoveLegacyHeader();
            EnsureListChrome();
            if (_contentHolder.GetComponent<Graphic>() is Image rootBg)
            {
                rootBg.enabled = true;
                TrucoMenuTheme.Surface(rootBg, TrucoClubUI.Ink, false);
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

        var background = _contentHolder.GetComponent<Image>();
        if (background != null) TrucoMenuTheme.Surface(background, TrucoClubUI.Ink, false);
        var header = TrucoClubUI.Panel(HeaderRootName, holder, TrucoMenuTheme.Brown,
            new Vector2(0, 1), Vector2.one);
        TrucoMenuTheme.Surface(header, TrucoMenuTheme.Brown, false);
        header.raycastTarget = false;
        var headerRt = header.rectTransform;
        headerRt.SetAsFirstSibling(); headerRt.pivot = new Vector2(.5f, 1);
        headerRt.sizeDelta = new Vector2(0, _headerBlockHeight);
        headerRt.anchoredPosition = Vector2.zero;

        if (TrucoClubUI.Hero != null)
        {
            var hero = TrucoClubUI.Panel("TournamentHero", header.transform, Color.white,
                new Vector2(.45f, .04f), new Vector2(.99f, .96f));
            hero.sprite = TrucoClubUI.Hero; hero.type = Image.Type.Simple;
            hero.preserveAspect = true; hero.raycastTarget = false;
        }

        var back = TrucoClubUI.Button(header.transform, "←", () => MainMenuViewCoordinator.ReturnToMenuFromTournament(),
            new Vector2(.035f, .71f), new Vector2(.14f, .96f), true);
        back.name = "Back";
        TrucoMenuTheme.Surface(back.GetComponent<Image>(), TrucoMenuTheme.DeepBrown);
        var backLabel = back.GetComponentInChildren<TextMeshProUGUI>();
        backLabel.fontSize = 44; backLabel.fontSizeMax = 44; backLabel.fontSizeMin = 36;
        backLabel.color = TrucoMenuTheme.Gold;

        var title = TrucoClubUI.Text(header.transform,
            string.IsNullOrWhiteSpace(_headerTitle) ? "TORNEOS" : _headerTitle.Trim().ToUpperInvariant(),
            49, TrucoMenuTheme.Gold, new Vector2(.045f, .35f), new Vector2(.51f, .71f), true);
        title.name = "Encabezado";
        title.characterSpacing = 1.2f;
        var subtitle = TrucoClubUI.Text(header.transform, "Tu próxima gran mesa", 27, TrucoMenuTheme.Card,
            new Vector2(.045f, .15f), new Vector2(.51f, .37f));
        subtitle.name = "Activos";
        var rule = TrucoClubUI.Panel("HeaderRule", header.transform, TrucoMenuTheme.Gold,
            Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
        rule.raycastTarget = false;

        var scroll = holder.Find("Scroll View") as RectTransform;
        if (scroll != null)
        {
            scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
            scroll.pivot = new Vector2(.5f, .5f);
            scroll.offsetMin = new Vector2(20, 218);
            scroll.offsetMax = new Vector2(-20, -_headerBlockHeight - 12);
        }
        if (_tournamentsCardHolder != null)
        {
            var layout = _tournamentsCardHolder.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 26; layout.padding = new RectOffset(28, 28, 24, 24);
                layout.childControlHeight = layout.childControlWidth = true;
                layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }

    public void DisplayTournamentSelectionUI(Tournament[] activeTournaments)
    {
        if (_contentHolder == null) return;
        if (_tournamentsCardHolder == null) return;
        EnsureListChrome();

        foreach (var card in _instantiatedCards) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
        _instantiatedCards.Clear();
        var seen = new HashSet<string>();

        foreach (var t in activeTournaments ?? new Tournament[0])
        {
            if (t == null || string.IsNullOrWhiteSpace(t._id) || !seen.Add(t._id)) continue;
            var tournamentCard = Instantiate(_tournamentCardPF, _tournamentsCardHolder);
            tournamentCard.Init(t, _woodTopPanelSprite);
            _instantiatedCards.Add(tournamentCard);
        }

        _contentHolder.gameObject.SetActive(true);
        MainMenuViewCoordinator.EnsureTournamentNavPriority();

        LayoutRebuilder.ForceRebuildLayoutImmediate(_tournamentsCardHolder);
        Canvas.ForceUpdateCanvases();
        var empty = _contentHolder.transform.Find("EmptyState");
        if (empty == null) empty = TrucoClubUI.Text(_contentHolder.transform,
            "NO HAY TORNEOS ABIERTOS\nVolvé pronto para encontrar tu próxima mesa.", 32, TrucoClubUI.Cream,
            new Vector2(.08f, .35f), new Vector2(.92f, .6f), true, TextAlignmentOptions.Center).transform;
        empty.name = "EmptyState"; empty.gameObject.SetActive(_instantiatedCards.Count == 0);
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
