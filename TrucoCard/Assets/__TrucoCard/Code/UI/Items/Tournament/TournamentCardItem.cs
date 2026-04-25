using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MH.Multiplayer;
using System.Text;
using System.Globalization;
using System;

public class TournamentCardItem : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _rulesText;

    [SerializeField] private Button _joinTournamentBtn;

    [Header("Read-Only")]
    [SerializeField] private Tournament _iTournamentData;

    #endregion

    public void Init(Tournament tournament)
    {
        _iTournamentData = tournament;

        if (_iTournamentData == null)
        {
            _titleText.SetText(string.Empty);
            _descriptionText.SetText(string.Empty);
            _rulesText.SetText(string.Empty);
            _joinTournamentBtn.onClick.RemoveAllListeners();
            return;
        }

        _titleText.SetText(_iTournamentData.name);
        _descriptionText.SetText(_iTournamentData.description);

        // Colors (hex) for label, value, and missing value
        const string labelColor = "#9AA0A6";       // gray for labels
        const string valueColor = "#00D1FF";       // cyan for normal values
        const string missingValueColor = "#FFA500"; // orange for missing values

        // Handle possible null or empty dates with ISO parsing and formatting
        string startDateText = string.IsNullOrWhiteSpace(_iTournamentData.startDate)
            ? "TBD"
            : FormatIsoDate(_iTournamentData.startDate);

        bool endDateMissing = string.IsNullOrWhiteSpace(_iTournamentData.endDate);
        string endDateText = endDateMissing
            ? "Not set"
            : FormatIsoDate(_iTournamentData.endDate);

        // Build the rules string with color tags
        var sb = new StringBuilder();
        sb.AppendFormat("<color={0}>Max Player:</color> <color={1}>{2}</color>\n", labelColor, valueColor, _iTournamentData.maxPlayers);
        sb.AppendFormat("<color={0}>Entry Fee:</color> <color={1}>{2}</color>\n", labelColor, valueColor, _iTournamentData.entryFee);
        sb.AppendFormat("<color={0}>Privacy:</color> <color={1}>{2}</color>\n", labelColor, valueColor, _iTournamentData.type);
        sb.AppendFormat("<color={0}>Start Date:</color> <color={1}>{2}</color>\n", labelColor, valueColor, startDateText);

        // Use a different color when end date is missing
        string endValueColor = endDateMissing ? missingValueColor : valueColor;
        sb.AppendFormat("<color={0}>End Date:</color> <color={1}>{2}</color>", labelColor, endValueColor, endDateText);

        _rulesText.SetText(sb.ToString());

        _joinTournamentBtn.onClick.RemoveAllListeners();
        _joinTournamentBtn.onClick.AddListener(() =>
        {
            JoinTournamentBtn_fn();
        });

    }

    private static string FormatIsoDate(string iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return iso;

        // Try to parse ISO 8601 strings (with Z or timezone)
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dt))
        {
            // If time is exactly midnight, show only date
            if (dt.TimeOfDay == TimeSpan.Zero)
                return dt.ToString("yyyy-MM-dd");

            // If seconds are zero, show up to minutes; otherwise include seconds
            if (dt.Second == 0)
                return dt.ToString("yyyy-MM-dd HH:mm");
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // Fallback: if string contains 'T', return the part before it (date portion)
        int tIndex = iso.IndexOf('T');
        if (tIndex > 0)
            return iso.Substring(0, tIndex);

        // As a last resort return the original string
        return iso;
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