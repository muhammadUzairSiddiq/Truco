using MH.Multiplayer;
using UnityEngine;

public class TournamentMatchmakingOverlay : MonoBehaviour
{
    [SerializeField] private RectTransform _contentHolder;
    TrucoThemeRuntime.Prompt _prompt;
    bool _pending;

    public async void DisplayTournamentMatchmakingUI(TournamentRequest request)
    {
        if (_pending || _contentHolder == null || request == null) return;
        _pending = true;
        string error = null;
        try
        {
            _contentHolder.gameObject.SetActive(true);
            _prompt ??= TrucoThemeRuntime.BuildPrompt(_contentHolder, TrucoCanvasOrder.Loading, loading: true);
            _prompt.Title.text = request.TournamentName;
            await MultiplayerController.InitMultiplayerSession_Tournament(request, message => error = message,
                message => _prompt.SetBody(message));
        }
        finally
        {
            _pending = false;
            if (this != null) HideTournamentMatchmakingUI();
        }
        if (this != null && error != null)
            AppManager.Instance?.DisplayNotification(error, title: "NO SE PUDO ENTRAR AL TORNEO");
    }

    public void HideTournamentMatchmakingUI()
    {
        if (_contentHolder != null) _contentHolder.gameObject.SetActive(false);
    }

    public void CancelTournmentMatchmaking()
    {
        if (!_pending) _ = MultiplayerController.LeaveTournamentByPlayerAsync(HideTournamentMatchmakingUI);
    }
}
