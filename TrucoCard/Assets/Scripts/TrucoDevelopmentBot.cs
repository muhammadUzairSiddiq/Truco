#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Drives the real shared UI/gameplay through Photon. Never impersonates a second local seat.</summary>
public sealed class TrucoDevelopmentBot : MonoBehaviourPunCallbacks
{
    static TrucoDevelopmentBot _instance;
    System.Random _random;
    bool _loading, _reported;
    int _actions;
    float _lastAction;
    string _room;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (!TrucoDevelopmentSettings.AIEnabled || _instance != null) return;
        var go = new GameObject("[Development AI]"); DontDestroyOnLoad(go); _instance = go.AddComponent<TrucoDevelopmentBot>();
    }
    void Start()
    {
        var settings = TrucoDevelopmentSettings.Current;
        _random = new System.Random(settings.seed);
        _lastAction = Time.realtimeSinceStartup;
        if (Environment.GetCommandLineArgs().Contains("-truco-ai")) StartTestPeer();
    }
    public static void StartTestPeer()
    {
        AutoAttach();
        if (_instance == null) { Debug.LogError("Select DevelopmentAI in the Editor development settings first."); return; }
        if (PhotonNetwork.IsConnected) { Debug.LogError("Disconnect the current session before starting an isolated AI peer."); return; }
        var config = TrucoDevelopmentSettings.Current;
        var stagingSettings = TrucoDevelopmentSettings.CreateStagingPhotonSettings();
        if (stagingSettings == null)
        { Debug.LogError("Set a separate stagingPhotonAppId in TrucoDevelopmentSettings before connecting synthetic AI peers."); return; }
        _instance._room = config.roomPrefix + config.testRoom;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "truco-ai-development-v1";
        PhotonNetwork.NickName = "AI-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        PhotonNetwork.AuthValues = new AuthenticationValues(PhotonNetwork.NickName);
        OneVsOneMatchSession.Clear();
        OneVsOneMatchSession.SetWithFlor(config.withFlor); OneVsOneMatchSession.SetTargetScore(config.targetScore);
        TrucoMatchProgress.ResetForNewMatch();
        PhotonNetwork.ConnectUsingSettings(stagingSettings);
    }
    public override void OnConnectedToMaster()
    {
        if (string.IsNullOrEmpty(_room)) return;
        var config = TrucoDevelopmentSettings.Current;
        PhotonNetwork.JoinOrCreateRoom(_room, new RoomOptions { MaxPlayers = 2, IsVisible = false,
            PlayerTtl = 60000, EmptyRoomTtl = 1000, PublishUserId = true,
            CustomRoomProperties = new Hashtable { ["withFlor"] = config.withFlor, ["targetScore"] = config.targetScore, ["developmentAI"] = true }
        }, TypedLobby.Default);
    }
    public override void OnJoinedRoom()
    {
        if (!TrucoDevelopmentSettings.CanDriveRoom(PhotonNetwork.CurrentRoom?.Name)) return;
        TrucoRoomPersistence.SaveCurrentRoom();
        TryLoad();
    }
    public override void OnPlayerEnteredRoom(Player player) => TryLoad();
    void TryLoad()
    {
        if (_loading || !TrucoDevelopmentSettings.CanDriveRoom(PhotonNetwork.CurrentRoom?.Name)
            || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount != 2) return;
        _loading = true; PhotonNetwork.LoadLevel("Gameplay");
    }
    IEnumerator Act()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(TrucoDevelopmentSettings.Current.actionInterval);
            if (!TrucoDevelopmentSettings.CanDriveRoom(PhotonNetwork.CurrentRoom?.Name) || SceneManager.GetActiveScene().name != "Gameplay") continue;
            var game = GameManager.Instance; var ui = UIMANAGER.Instance;
            if (game == null || ui == null) continue;
            if (game._gameEnded)
            {
                if (!_reported)
                {
                    _reported = true;
                    Debug.Log($"[TRUCO AI RESULT] actions={_actions} score={DataHandler.Instance.points}:{DataHandler.Instance.opponentPoints} seed={TrucoDevelopmentSettings.Current.seed}");
                    if (Environment.GetCommandLineArgs().Contains("-truco-ai")) Application.Quit(0);
                }
                continue;
            }
            if (ui.TryDevelopmentAction(_random)) { _actions++; _lastAction = Time.realtimeSinceStartup; }
            else if (game.IsMyTurn() && game.CanPlayCard())
            {
                var cards = ui.myPlayerCards.Where(c => c != null && c.activeInHierarchy).Select(c => c.GetComponent<Card>())
                    .Where(c => c != null && c.CanUserSelect()).OrderBy(c => TrucoCardRules.TrucoRank(c.value, c.suit)).ToList();
                if (cards.Count > 0) { cards[_random.Next(cards.Count)].CommitPlayForTimeout(); _actions++; _lastAction = Time.realtimeSinceStartup; }
            }
            if (Time.realtimeSinceStartup - _lastAction > 45) { Debug.LogError("[TRUCO AI STALL] No legal progress for 45 seconds; not a passed match."); _lastAction = Time.realtimeSinceStartup; }
        }
    }
    public override void OnEnable() { base.OnEnable(); StartCoroutine(Act()); }
    public override void OnDisable() { StopAllCoroutines(); base.OnDisable(); }
}
#endif
