using MH.Multiplayer;
using Photon.Pun;
using TMPro;
using UnityEngine;

public enum TrucoTournamentPhase { None, Filling, CreatingMatches, LoadingTable, Playing, WaitingNextRound, Completed, Failed, Cancelled, Reconciling }

/// <summary>Lifecycle presentation driven by actual controller callbacks, never by a success timer.</summary>
public sealed class TrucoTournamentStatusScreen : MonoBehaviour
{
    static TrucoTournamentStatusScreen _instance;
    TextMeshProUGUI _title, _body, _seats;
    public static TrucoTournamentPhase Phase { get; private set; }
    public static string Detail { get; private set; }
    public static void SetPhase(TrucoTournamentPhase phase, string detail = null)
    {
        Phase = phase; Detail = detail;
        if (phase == TrucoTournamentPhase.None || phase == TrucoTournamentPhase.Playing || phase == TrucoTournamentPhase.Completed)
        { Hide(); return; }
        if (_instance == null)
        {
            var go = new GameObject("TournamentStatus"); DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrucoTournamentStatusScreen>(); _instance.Build();
        }
        _instance.gameObject.SetActive(true); _instance.Refresh();
    }
    public static void Hide() { if (_instance != null) _instance.gameObject.SetActive(false); }
    void Build()
    {
        var safe = TrucoClubUI.Screen(gameObject, TrucoCanvasOrder.TournamentStatus);
        _title = TrucoClubUI.Text(safe, "TORNEO", 46, TrucoClubUI.Gold, new Vector2(.06f, .87f), new Vector2(.94f, .97f), true);
        var hero = TrucoClubUI.Panel("Hero", safe, Color.white, new Vector2(.06f, .56f), new Vector2(.94f, .86f)); hero.sprite = TrucoClubUI.Hero; hero.preserveAspect = true;
        var status = TrucoClubUI.Panel("StatusCard", safe, TrucoClubUI.Surface, new Vector2(.06f, .2f), new Vector2(.94f, .53f));
        _body = TrucoClubUI.Text(status.transform, "", 35, TrucoClubUI.Gold, new Vector2(.02f, .43f), new Vector2(.98f, .97f), true);
        _seats = TrucoClubUI.Text(status.transform, "", 28, TrucoClubUI.Cream, new Vector2(.02f, .03f), new Vector2(.98f, .43f));
        TrucoClubUI.Button(safe, "VER CUADRO", () => { if (MultiplayerController.BracketState != null) TournamentBracketOverlay.Show(false); }, new Vector2(.06f, .1f), new Vector2(.94f, .17f), true);
        TrucoClubUI.Button(safe, "VOLVER AL INICIO", () => TrucoConfirmDialog.Show("SALIR DEL TORNEO", "Podés volver al inicio. El servidor debe confirmar cualquier cancelación o reembolso.", "SEGUIR AQUÍ", "VOLVER",
            () => { _ = MultiplayerController.LeaveTournamentByPlayerAsync(() => { Hide(); TrucoSceneTransition.Go("MainMenu"); }); }, () => { }), new Vector2(.06f, .02f), new Vector2(.94f, .09f));
        if (Application.isPlaying) InvokeRepeating(nameof(Refresh), 0, .5f);
    }
    void Refresh()
    {
        _title.text = MultiplayerController.CurrentTournamentRequest?.TournamentName ?? "TORNEO";
        switch (Phase)
        {
            case TrucoTournamentPhase.Filling: _body.text = "INSCRIPCIÓN CONFIRMADA\nEsperando a los demás jugadores…"; break;
            case TrucoTournamentPhase.CreatingMatches: _body.text = "PREPARANDO LAS MESAS\nConfirmando las partidas con el servidor…"; break;
            case TrucoTournamentPhase.LoadingTable: _body.text = "TU MESA ESTÁ EN CAMINO\nEsperando que ambos jugadores estén listos…"; break;
            case TrucoTournamentPhase.WaitingNextRound: _body.text = "CLASIFICASTE\nEsperando al ganador de la otra partida…"; break;
            case TrucoTournamentPhase.Failed: _body.text = "NO SE PUDO INICIAR\n" + Detail; break;
            case TrucoTournamentPhase.Cancelled: _body.text = "TORNEO CANCELADO\n" + Detail; break;
            case TrucoTournamentPhase.Reconciling: _body.text = "CONFIRMANDO ESTADO\n" + Detail; break;
        }
        var request = MultiplayerController.CurrentTournamentRequest;
        _seats.text = request == null ? "La inscripción y el reembolso requieren confirmación del servidor." :
            $"{(request.WithFlor ? "Con Flor" : "Sin Flor")}  ·  {request.TargetScore} puntos\n{request.MaxPlayerCount} jugadores  ·  Premio previsto: {request.Prize} Trucoins";
        if (Phase == TrucoTournamentPhase.Filling && PhotonNetwork.CurrentRoom != null)
            _seats.text += $"\nConectados: {PhotonNetwork.CurrentRoom.PlayerCount}/{request?.MaxPlayerCount}";
    }
#if UNITY_EDITOR
    public static GameObject EditorPreview(TrucoTournamentPhase phase, string detail = null)
    {
        var root = new GameObject("TournamentStatusEditorPreview");
        var preview = root.AddComponent<TrucoTournamentStatusScreen>();
        preview.Build();
        var previousPhase = Phase; var previousDetail = Detail;
        Phase = phase; Detail = detail; preview.Refresh();
        Phase = previousPhase; Detail = previousDetail;
        return root;
    }
#endif
}
