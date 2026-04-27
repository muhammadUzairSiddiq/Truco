using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MH.Multiplayer;
using System.Text;

public class TournamentCardItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _rulesText;
    [SerializeField] private Button _joinTournamentBtn;

    [Header("Read-Only")]
    [SerializeField] private Tournament _iTournamentData;

    /// <summary>On brown panel: labels + values readable (no blue block).</summary>
    const string L = "<color=#E8D4B8>";
    const string V = "<color=#FFFDF8>";
    const string A = "<color=#F5D76E>";

    public void Init(Tournament tournament, Sprite woodPanelSprite = null)
    {
        _iTournamentData = tournament;
        if (_descriptionText != null) _descriptionText.richText = true;
        if (_rulesText != null) _rulesText.richText = true;

        if (_iTournamentData == null)
        {
            if (_titleText != null) _titleText.SetText(string.Empty);
            if (_descriptionText != null) _descriptionText.SetText(string.Empty);
            if (_rulesText != null) _rulesText.SetText(string.Empty);
            if (_joinTournamentBtn != null) _joinTournamentBtn.onClick.RemoveAllListeners();
            return;
        }

        if (_titleText != null)
        {
            _titleText.SetText(_iTournamentData.name);
            _titleText.color = new Color(0.98f, 0.95f, 0.88f, 1f);
        }

        if (_descriptionText != null) _descriptionText.SetText(string.Empty);
        var descBlock = transform.Find("DescriptionBG");
        if (descBlock != null) descBlock.gameObject.SetActive(false);

        int n = 0;
        if (_iTournamentData.participants != null) n = _iTournamentData.participants.Count;

        string typeLabel;
        if (string.IsNullOrEmpty(_iTournamentData.type)) typeLabel = "—";
        else
        {
            var ty = _iTournamentData.type.ToLowerInvariant();
            if (ty == "public" || ty == "público") typeLabel = "PÚBLICO";
            else if (ty == "private" || ty == "privado") typeLabel = "PRIVADO";
            else typeLabel = _iTournamentData.type.ToUpperInvariant();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{L}Ingreso:</color> {V}{_iTournamentData.entryFee} Trucoins</color>");
        sb.AppendLine($"{L}Premio:</color> {V}{_iTournamentData.prizePool} Trucoins</color>");
        sb.AppendLine($"{L}Tamaño:</color> {V}{_iTournamentData.maxPlayers} Jugadores</color>");
        sb.AppendLine($"{L}Jugadores inscritos:</color> {V}{n} / {_iTournamentData.maxPlayers}</color>");
        sb.AppendLine($"{L}Puntos:</color> {V}—</color>");
        sb.AppendLine($"{L}Flor:</color> {V}Con Flor</color>");
        sb.Append($"\n<align=center>{A}[{typeLabel}]</align></color>");

        if (_rulesText != null)
        {
            _rulesText.SetText(sb.ToString());
            _rulesText.color = Color.white;
        }

        EnsureLargeCardLayout();
        ApplyWoodTheme(woodPanelSprite);
        StyleRegisterButton();

        if (_joinTournamentBtn == null) return;
        _joinTournamentBtn.onClick.RemoveAllListeners();
        _joinTournamentBtn.onClick.AddListener(JoinTournamentBtn_fn);
    }

    void EnsureLargeCardLayout()
    {
        // ~1 tarjeta visible a la vez en móvil vertical (Game ref: zona roja).
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.minWidth = 920f;
        le.minHeight = 1540f;
        le.preferredWidth = 960f;
        le.preferredHeight = 1600f;

        var rulesBg = transform.Find("RulesBG");
        if (rulesBg != null)
        {
            var rle = rulesBg.GetComponent<LayoutElement>();
            if (rle == null) rle = rulesBg.gameObject.AddComponent<LayoutElement>();
            rle.minHeight = 900f;
        }
    }

    void ApplyWoodTheme(Sprite woodPanelSprite)
    {
        var brownPanel = new Color(0.48f, 0.35f, 0.25f, 0.96f);
        var titleStrip = new Color(0.55f, 0.42f, 0.3f, 1f);
        var rulesPanel = new Color(0.4f, 0.3f, 0.22f, 0.95f);
        var frame = new Color(0.32f, 0.22f, 0.16f, 0.28f);

        var imgs = GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img == null) continue;
            if (!img.gameObject.activeInHierarchy) continue;
            if (img.gameObject == _joinTournamentBtn?.gameObject) continue;
            var n = img.gameObject.name;
            if (woodPanelSprite != null && (n == "TitleBG" || n == "DescriptionBG" || n == "RulesBG"))
            {
                img.sprite = woodPanelSprite;
                img.type = Image.Type.Sliced;
            }
            if (n == "TitleBG")
                img.color = titleStrip;
            else if (n == "DescriptionBG")
                img.color = new Color(0.88f, 0.8f, 0.68f, 0.9f);
            else if (n == "RulesBG")
                img.color = rulesPanel;
        }

        var selfImg = GetComponent<Image>();
        if (selfImg != null)
        {
            if (woodPanelSprite != null)
            {
                selfImg.sprite = woodPanelSprite;
                selfImg.type = Image.Type.Sliced;
            }
            selfImg.color = frame;
        }
    }

    void StyleRegisterButton()
    {
        if (_joinTournamentBtn == null) return;
        var g = _joinTournamentBtn.GetComponent<Image>();
        if (g != null) g.color = Color.white;
        var c = _joinTournamentBtn.colors;
        var baseC = TrucoUiTheme.TournamentRegisterButton;
        c.colorMultiplier = 1f;
        c.normalColor = baseC;
        c.highlightedColor = Color.Lerp(baseC, Color.white, 0.12f);
        c.pressedColor = Color.Lerp(baseC, Color.black, 0.12f);
        c.selectedColor = baseC;
        _joinTournamentBtn.colors = c;

        var t = _joinTournamentBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            t.text = "INSCRIBIRSE";
            t.color = Color.white;
            t.fontStyle = FontStyles.Bold;
        }
    }

    private void JoinTournamentBtn_fn()
    {
        if (ApiController.GetSessionUser.Data.wallet.balance < _iTournamentData.entryFee)
        {
            AppManager.Instance.DisplayNotification("Insufficient balance to join this tournament.");
            return;
        }

        if (_iTournamentData.type == "private")
        {
            TournamentManager.Instance.DisplayTournamentPasswordInputOverlay(_iTournamentData._id, () =>
            {
                DoMatchMaking();
            });
        }
        else
        {
            DoMatchMaking();
        }
    }

    private void DoMatchMaking()
    {
        TournamentRequest request = new TournamentRequest
        (
            _iTournamentData._id,
            _iTournamentData.maxPlayers,
            _iTournamentData.entryFee,
            _iTournamentData.name
        );

        TournamentManager.Instance.DisplayTournamentMatchmakingUI(request);
    }
}
