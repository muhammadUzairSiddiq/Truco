using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editor-only interactive tournament UI. Every result comes from played practice cards.</summary>
public static class TrucoTournamentPracticeUI
{
    static GameObject _root;
    static RectTransform _safe;
    static TrucoPracticeTournament _tournament;
    static TrucoPracticeMatch _match;
    static int _capacity = 4, _round, _index;
    static string _opponent;
    static string _page;
    static double _nextAction;
    public static GameObject Root => _root;
    static TrucoTournamentPracticeSettings Settings => TrucoTournamentPracticeSettings.Load();
    public static void Close()
    {
        EditorApplication.update -= Tick; TrucoMenuTheme.DestroyUI(_root); _root = null;
        _match = null; _tournament = null;
        if (Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            MainMenuViewCoordinator.ReturnToMenuFromTournament();
    }
    static void Page(string name, string title, string subtitle = null)
    {
        if (Application.isPlaying && UnityEngine.EventSystems.EventSystem.current == null)
        {
            var events = new GameObject("EditorPracticeEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            UnityEngine.Object.DontDestroyOnLoad(events);
        }
        TrucoMenuTheme.DestroyUI(_root);
        _root = new GameObject("EditorTournamentPractice"); _root.hideFlags = HideFlags.DontSave;
        if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_root);
        _safe = TrucoClubUI.Screen(_root, TrucoCanvasOrder.EditorPractice); _page = name;
        TrucoMenuTheme.Header(_safe, title, subtitle ?? "PRÁCTICA EDITOR · SIN SALDOS NI PREMIOS REALES");
        TrucoClubUI.Button(_safe, "CERRAR PRÁCTICA", Close, new Vector2(.06f, .015f), new Vector2(.94f, .075f), true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        _nextAction = EditorApplication.timeSinceStartup + Math.Max(.05, Settings != null ? Settings.aiActionDelay : .45);
    }
    public static void OpenList()
    {
        if (Application.isPlaying)
        {
            // EnterPlayMode/direct Editor entry can bypass the normal sceneLoaded fade.
            // Practice is ready immediately; release that cover so its controls receive input.
            TrucoSceneTransition.ReleaseCover();
            TrucoSceneTransition.Ensure().FadeIn(.15f);
        }
        _tournament = null; _match = null;
        Page("list", "TORNEOS", "PRÁCTICA EDITOR · Jugá sin conexión al backend");
        var hero = TrucoClubUI.Panel("Hero", _safe, Color.white, new Vector2(.06f, .61f), new Vector2(.94f, .85f)); hero.sprite = TrucoClubUI.Hero; hero.preserveAspect = true;
        TrucoClubUI.Text(hero.transform, "TU PRÓXIMA\nGRAN MESA", 42, TrucoClubUI.Cream, new Vector2(.03f, .2f), new Vector2(.58f, .95f), true);
        for (int i = 0; i < 2; i++)
        {
            int count = i == 0 ? 4 : 8; float y = i == 0 ? .36f : .12f;
            var card = TrucoClubUI.Panel("PracticeTournament" + count, _safe, TrucoClubUI.Surface, new Vector2(.06f, y), new Vector2(.94f, y + .22f));
            TrucoClubUI.Text(card.transform, count == 4 ? "COPA DE CUATRO" : "GRAN TORNEO DE OCHO", 35, TrucoClubUI.Gold, new Vector2(.025f, .66f), new Vector2(.975f, .96f), true);
            TrucoClubUI.Text(card.transform, $"{count} jugadores · {Rules()}\nVos + {count - 1} oponentes AI · entrada sin costo", 28, TrucoClubUI.Cream, new Vector2(.025f, .34f), new Vector2(.975f, .67f));
            TrucoClubUI.Button(card.transform, "VER TORNEO  ›", () => Details(count), new Vector2(.04f, .035f), new Vector2(.96f, .32f));
        }
    }
    static string Rules() => $"{(Settings == null || Settings.withFlor ? "Con Flor" : "Sin Flor")} · {TrucoMatchRules.NormalizeTargetScore(Settings != null ? Settings.targetScore : 15)} puntos";
    public static void Details(int count)
    {
        _capacity = count;
        Page("details", "DETALLES DEL TORNEO");
        var hero = TrucoClubUI.Panel("Hero", _safe, Color.white, new Vector2(.06f, .59f), new Vector2(.94f, .85f)); hero.sprite = TrucoClubUI.Hero; hero.preserveAspect = true;
        var info = TrucoClubUI.Panel("TournamentDetails", _safe, TrucoClubUI.Surface, new Vector2(.06f, .25f), new Vector2(.94f, .57f));
        TrucoClubUI.Text(info.transform, $"COPA DE {count} JUGADORES", 39, TrucoClubUI.Gold, new Vector2(.025f, .73f), new Vector2(.975f, .98f), true);
        TrucoClubUI.Text(info.transform, Rules() + $"\n{count - 1} rivales controlados por AI", 31, TrucoClubUI.Cream, new Vector2(.025f, .43f), new Vector2(.975f, .74f));
        TrucoClubUI.Text(info.transform, "TU RECORRIDO\n" + (count == 4 ? "Semifinales → Final" : "Cuartos → Semifinales → Final"), 29, TrucoClubUI.Cream, new Vector2(.025f, .16f), new Vector2(.975f, .44f), true);
        TrucoClubUI.Text(info.transform, "Entrada gratuita · Sin movimientos de saldo", 25, TrucoClubUI.Muted, new Vector2(.025f, .01f), new Vector2(.975f, .16f));
        TrucoClubUI.Button(_safe, "ENTRAR A LA PRÁCTICA", StartTournament, new Vector2(.06f, .13f), new Vector2(.94f, .22f));
    }
    static void StartTournament()
    {
        _tournament = new TrucoPracticeTournament(_capacity, Settings != null ? Settings.targetScore : 15, Settings == null || Settings.withFlor, Settings != null ? Settings.seed : 1731);
        _round = _index = 0; _match = null; Waiting();
    }
    public static void Waiting()
    {
        Page("waiting", "LA MESA TE ESPERA");
        var hero = TrucoClubUI.Panel("Hero", _safe, Color.white, new Vector2(.06f, .55f), new Vector2(.94f, .85f)); hero.sprite = TrucoClubUI.Hero; hero.preserveAspect = true;
        var ready = TrucoClubUI.Panel("ReadySummary", _safe, TrucoClubUI.Surface, new Vector2(.06f, .26f), new Vector2(.94f, .53f));
        TrucoClubUI.Text(ready.transform, _round == 0 ? "TODO LISTO PARA EMPEZAR" : "CLASIFICASTE A LA PRÓXIMA RONDA", 36, TrucoClubUI.Gold, new Vector2(.02f, .69f), new Vector2(.98f, .98f), true);
        TrucoClubUI.Text(ready.transform, $"{_capacity} participantes · Rivales listos\n{Rules()}\n\nLos resultados se deciden jugando las cartas.", 30, TrucoClubUI.Cream, new Vector2(.02f, .05f), new Vector2(.98f, .69f));
        TrucoClubUI.Button(_safe, "VER CUADRO", Bracket, new Vector2(.06f, .14f), new Vector2(.94f, .22f));
    }
    public static void Bracket()
    {
        if (_tournament == null) { StartTournament(); return; }
        Page("bracket", "CUADRO DEL TORNEO", $"{_capacity} jugadores · {Rules()} · Práctica Editor");
        var scroll = TrucoClubUI.Panel("BracketViewport", _safe, new Color(0, 0, 0, .12f), new Vector2(.03f, .2f), new Vector2(.97f, .85f)).gameObject.AddComponent<ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", scroll.transform, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        var content = TrucoClubUI.Rect("Bracket", viewport, new Vector2(0, 1), new Vector2(0, 1)); content.pivot = new Vector2(0, 1);
        TournamentBracketGraphic.Build(content, _tournament.Bracket, TrucoPracticeTournament.LocalId);
        scroll.viewport = viewport; scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
        TrucoClubUI.Button(_safe, "JUGAR MI PARTIDA", OpenTable, new Vector2(.06f, .1f), new Vector2(.94f, .18f));
    }
    public static void OpenTable()
    {
        if (_tournament == null) StartTournament();
        var table = _tournament.Bracket.rounds[_round].FirstOrDefault(t => !t.IsFinished && (t.first.playerId == TrucoPracticeTournament.LocalId || t.second.playerId == TrucoPracticeTournament.LocalId));
        if (table == null) { FinishWithBots(); return; }
        _index = table.index; _opponent = table.first.playerId == TrucoPracticeTournament.LocalId ? table.second.playerName : table.first.playerName;
        if (_match == null) _match = new TrucoPracticeMatch(_tournament.Seed + _round * 101 + _index, _tournament.Target, _tournament.WithFlor);
        Table();
    }
    static void Table()
    {
        Page("table", _tournament.Bracket.RoundTitle(_round), "PRÁCTICA EDITOR · " + Rules());
        var score = TrucoClubUI.Panel("Score", _safe, TrucoClubUI.Surface, new Vector2(.06f, .77f), new Vector2(.94f, .85f));
        TrucoClubUI.Text(score.transform, $"VOS  {_match.Scores[0]}     :     {_match.Scores[1]}  {_opponent}", 29, TrucoClubUI.Gold, Vector2.zero, Vector2.one, true, TextAlignmentOptions.Center);
        var board = TrucoClubUI.Panel("Table", _safe, new Color32(25, 76, 52, 255), new Vector2(.06f, .4f), new Vector2(.94f, .70f));
        TrucoClubUI.Text(board.transform, "MANO " + _match.HandNumber + "  ·  VALE " + _match.Stake, 22, TrucoClubUI.Muted, new Vector2(0, .82f), Vector2.one, false, TextAlignmentOptions.Center);
        for (int actor = 0; actor < 2; actor++)
        {
            int current = actor;
            var card = TrucoClubUI.Panel("PlayedCard" + actor, board.transform, _match.Table[actor] != null ? Color.white : new Color(1, 1, 1, .08f), new Vector2(.16f + actor * .37f, .13f), new Vector2(.44f + actor * .37f, .79f));
            card.sprite = TrucoEditorCardArt.Face(_match.Table[actor]); card.preserveAspect = true;
            if (_match.Table[actor] == null) TrucoClubUI.Text(card.transform, actor == 0 ? "TU CARTA" : "RIVAL", 20, TrucoClubUI.Muted, Vector2.zero, Vector2.one, false, TextAlignmentOptions.Center);
        }
        TrucoClubUI.Text(_safe, _match.Pending != TrucoPracticeMatch.Canto.None ? _match.Message + (_match.Responder == 0 ? " · ¿Querés?" : " · El rival responde…") : _match.Message + "\n" + (_match.Turn == 0 ? "TU TURNO" : "TURNO DEL RIVAL"), 27, TrucoClubUI.Cream, new Vector2(.06f, .32f), new Vector2(.94f, .4f), true, TextAlignmentOptions.Center);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var face = TrucoClubUI.Panel("HandCard" + i, _safe, _match.Used[0][i] ? new Color(1, 1, 1, .22f) : Color.white,
                new Vector2(.14f + i * .25f, .16f), new Vector2(.36f + i * .25f, .315f));
            face.sprite = TrucoEditorCardArt.Face(_match.Hands[0][i]); face.preserveAspect = true;
            var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
            button.interactable = _match.CanAct(0) && !_match.Used[0][i];
            button.onClick.AddListener(() => { if (_match.Play(0, index)) Table(); }); face.gameObject.AddComponent<TrucoButtonFeedback>();
        }
        if (_match.Pending != TrucoPracticeMatch.Canto.None && _match.Responder == 0)
        {
            TrucoClubUI.Button(_safe, "QUIERO", () => { _match.Reply(0, true); Table(); }, new Vector2(.06f, .09f), new Vector2(.5f, .155f));
            TrucoClubUI.Button(_safe, "NO QUIERO", () => { _match.Reply(0, false); Table(); }, new Vector2(.5f, .09f), new Vector2(.94f, .155f), true);
        }
        else
        {
            var truco = TrucoClubUI.Button(_safe, "TRUCO", () => { _match.CallTruco(0); Table(); }, new Vector2(.06f, .09f), new Vector2(.35f, .155f)); truco.interactable = _match.CanTruco(0);
            var envido = TrucoClubUI.Button(_safe, "ENVIDO", () => { _match.CallEnvido(0); Table(); }, new Vector2(.35f, .09f), new Vector2(.65f, .155f)); envido.interactable = _match.CanEnvido(0);
            var fold = TrucoClubUI.Button(_safe, "MAZO", () => { _match.Fold(0); Table(); }, new Vector2(.65f, .09f), new Vector2(.94f, .155f), true); fold.interactable = _match.CanAct(0);
        }
        var auto = TrucoClubUI.Button(_safe, "AI: " + (Settings != null && Settings.autoPlayYourSeat ? "TODAS LAS CARTAS" : "SOLO RIVALES"), () => {
            if (Settings != null) { Settings.autoPlayYourSeat = !Settings.autoPlayYourSeat; EditorUtility.SetDirty(Settings); Table(); }
        }, new Vector2(.34f, .71f), new Vector2(.9f, .76f), true);
        auto.transform.SetAsLastSibling();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || _root == null || EditorApplication.timeSinceStartup < _nextAction) return;
        if (Settings == null || !Settings.enabledInEditor) { Close(); return; }
        _nextAction = EditorApplication.timeSinceStartup + Math.Max(.05, Settings != null ? Settings.aiActionDelay : .45);
        if (_page == "result" && Settings != null && Settings.autoPlayYourSeat) { Advance(); return; }
        if ((_page == "waiting" || _page == "bracket") && Settings != null && Settings.autoPlayYourSeat) { OpenTable(); return; }
        if (_page != "table" || _match == null) return;
        if (_match.Finished) { Result(); return; }
        if (_match.HandOver || _match.Responder == 1 || (_match.Pending == TrucoPracticeMatch.Canto.None && _match.Turn == 1) || Settings != null && Settings.autoPlayYourSeat)
        { _match.StepAI(); Table(); }
    }
    public static void Result()
    {
        Page("result", _match.Winner == 0 ? "¡GANASTE LA PARTIDA!" : "FIN DE TU PARTIDA");
        var summary = TrucoClubUI.Panel("MatchSummary", _safe, TrucoClubUI.Surface, new Vector2(.06f, .32f), new Vector2(.94f, .78f));
        TrucoClubUI.Text(summary.transform, "VOS                      RIVAL", 29, TrucoClubUI.Muted, new Vector2(.06f, .68f), new Vector2(.94f, .87f), true, TextAlignmentOptions.Center);
        TrucoClubUI.Text(summary.transform, $"{_match.Scores[0]}  :  {_match.Scores[1]}", 104, TrucoClubUI.Gold, new Vector2(.06f, .32f), new Vector2(.94f, .72f), true, TextAlignmentOptions.Center);
        TrucoClubUI.Text(summary.transform, _match.Winner == 0 ? "Clasificaste. Tu próximo rival sale de la otra mesa." : "Podés seguir el cuadro hasta conocer al campeón.", 32, TrucoClubUI.Cream, new Vector2(.06f, .04f), new Vector2(.94f, .33f), false, TextAlignmentOptions.Center);
        TrucoClubUI.Button(_safe, "CONTINUAR", Advance, new Vector2(.06f, .16f), new Vector2(.94f, .25f));
    }
    static void Advance()
    {
        var table = _tournament.Bracket.rounds[_round][_index];
        var rival = table.first.playerId == TrucoPracticeTournament.LocalId ? table.second : table.first;
        _tournament.Record(_round, _index, _match.Winner == 0 ? TrucoPracticeTournament.LocalId : rival.playerId,
            _match.Winner == 0 ? "Vos" : rival.playerName, _match.Actions);
        bool won = _match.Winner == 0; _match = null;
        _tournament.CompleteRoundWithBots(_round); _round++;
        if (_round >= _tournament.Bracket.rounds.Count || !won) { FinishWithBots(); return; }
        Waiting();
    }
    static void FinishWithBots() { _tournament.CompleteWithBots(); Champion(); }
    public static void Champion()
    {
        var final = _tournament.Bracket.rounds.Last()[0];
        Page("champion", "¡CAMPEÓN DEL TORNEO!");
        var trophy = TrucoClubUI.Panel("Trophy", _safe, Color.white, new Vector2(.06f, .53f), new Vector2(.94f, .84f)); trophy.sprite = TrucoClubUI.Hero; trophy.preserveAspect = true;
        TrucoClubUI.Text(_safe, final.winnerName, 58, TrucoClubUI.Gold, new Vector2(.06f, .42f), new Vector2(.94f, .53f), true, TextAlignmentOptions.Center);
        var completion = TrucoClubUI.Panel("TournamentCompletion", _safe, TrucoClubUI.Surface, new Vector2(.06f, .22f), new Vector2(.94f, .40f));
        TrucoClubUI.Text(completion.transform, $"{_tournament.CompletedMatches} partidas completadas\n{_tournament.Actions} acciones de juego\nPráctica sin premios ni movimientos de saldo", 30, TrucoClubUI.Cream, Vector2.zero, Vector2.one, false, TextAlignmentOptions.Center);
        TrucoClubUI.Button(_safe, "OTRO TORNEO", OpenList, new Vector2(.06f, .1f), new Vector2(.94f, .18f));
        if (Application.isPlaying) Debug.Log($"[EDITOR TOURNAMENT PRACTICE] players={_capacity} matches={_tournament.CompletedMatches} actions={_tournament.Actions} champion={final.winnerName}");
    }
    public static void PrepareGalleryTable(int capacity = 4)
    { _capacity = capacity; StartTournament(); OpenTable(); }
    public static void PrepareGalleryResult() { _match.CompleteWithBots(); Result(); }
    public static void PrepareGalleryChampion() { _tournament.CompleteWithBots(); Champion(); }
}
