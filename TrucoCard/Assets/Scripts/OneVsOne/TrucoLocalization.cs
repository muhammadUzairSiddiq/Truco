using System;
using UnityEngine;

/// <summary>Spanish / English UI strings. Persisted via PlayerPrefs; toggled from main menu ENG / SPN buttons.</summary>
public static class TrucoLocalization
{
    public enum Lang { Spanish = 0, English = 1 }

    const string PrefKey = "truco_ui_lang";

    static Lang _current = Lang.Spanish;
    static bool _loaded;

    public static Lang Current
    {
        get
        {
            if (!_loaded) Load();
            return _current;
        }
    }

    public static event Action OnLanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad()
    {
        Load();
        ForceSpanish();
    }

    public static void Load()
    {
        _loaded = true;
        _current = (Lang)PlayerPrefs.GetInt(PrefKey, (int)Lang.Spanish);
    }

    public static void SetLanguage(Lang lang)
    {
        lang = Lang.Spanish;
        if (!_loaded) Load();
        if (_current == lang) return;
        _current = lang;
        PlayerPrefs.SetInt(PrefKey, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    /// <summary>UI is Spanish-only; clears any saved English preference.</summary>
    public static void ForceSpanish()
    {
        _loaded = true;
        _current = Lang.Spanish;
        PlayerPrefs.SetInt(PrefKey, (int)Lang.Spanish);
        PlayerPrefs.Save();
    }

    public static bool IsEnglish => Current == Lang.English;

    public static string T(Key key) => Current == Lang.English ? En(key) : Es(key);

    // ───────────────────────── Spanish ─────────────────────────
    static string Es(Key k)
    {
        switch (k)
        {
            case Key.ImportantTitle: return "IMPORTANTE";
            case Key.Ok: return "ACEPTAR";
            case Key.Espere: return "Esperá…";
            case Key.Partida1v1: return "Partida 1 vs 1";
            case Key.SalasDisponibles: return "Salas disponibles";
            case Key.SeleccionarMesa: return "Seleccionar mesa";
            case Key.EntradaAbrev: return "Entrada";
            case Key.PremioAbrev: return "Premio";
            case Key.JugadoresEnSala: return "Jugadores: {0}/{1}";
            case Key.TuSalaEsperando: return "Tu sala";
            case Key.SalaLlena: return "Llena";
            case Key.SalaExpiradaEtiqueta: return "Expirado";
            case Key.SalaExpiradaAviso: return "La sala expiró. Creá o unite a otra sala.";
            case Key.CrearSala: return "Crear sala";
            case Key.ActualizarLista: return "Actualizar";
            case Key.Volver: return "Volver";
            case Key.BuscandoOponente: return "Buscando oponente…";
            case Key.EsperandoRivalSala: return "Esperando a otro jugador (2/2 para empezar)…";
            case Key.PartidaEncontrada: return "¡Oponente listo!";
            case Key.CargandoJuego: return "Cargando partida…";
            case Key.NombreSala: return "Nombre de la sala";
            case Key.TipoSala: return "Tipo de sala";
            case Key.Publica: return "Pública";
            case Key.Privada: return "Privada";
            case Key.ContrasenaSala: return "Contraseña";
            case Key.CodigoSala4: return "Código de sala (mín. 4 caracteres)";
            case Key.CodigoPrivadaInfo: return "Sala privada: elegí un código (mín. 4 caracteres). Solo vos lo verás en la lista.";
            case Key.CodigoInvalido4: return "Ingresá al menos 4 caracteres (contraseña de la sala).";
            case Key.CodigoEtiqueta: return "Cód.";
            case Key.TuCodigoSala: return "Tu código";
            case Key.CodigoParaUnir: return "Código";
            case Key.EntradaMonedas: return "Entrada (monedas)";
            case Key.Unirse: return "Unirse";
            case Key.Entrar: return "ENTRAR";
            case Key.SaldoInsuficiente: return "No tenés saldo suficiente para la entrada. Revisá en la tienda o recargá.";
            case Key.ErrorCrearSala: return "No se pudo crear la sala. Intentá otra vez.";
            case Key.ErrorUnirse: return "No se pudo unir a la sala.";
            case Key.Conectando: return "Conectando al servidor de partida…";
            case Key.IngresaContrasena: return "Ingresá la contraseña de la sala.";
            case Key.TiempoEsgotadoJugada: return "Se acabó el tiempo. Se juega la carta automáticamente.";
            case Key.CuentaRegresiva: return "Tiempo: {0} s";
            case Key.TuTurno: return "Tu turno";
            case Key.TurnoRival: return "Turno del rival";
            case Key.Responde: return "Respondé";
            case Key.EsperandoRespuestaRival: return "Esperando respuesta del rival…";
            case Key.EsperandoJugadaRival: return "Esperando jugada del rival…";
            case Key.TiempoRespuestaAgotado: return "Se acabó el tiempo para responder. Se rechaza el canto automáticamente.";
            case Key.GanastePremio: return "¡Ganaste {0} Trucoins de premio!";
            case Key.GanaPorAbandono: return "Ganaste: el rival dejó la partida.";
            case Key.RivalReconectando: return "El rival se desconectó. Reconectando…";
            case Key.Reconectando: return "Reconectando…";
            case Key.ReconectandoOverlay: return "Reconectando…";
            case Key.ReconectarFallo: return "No se pudo reconectar. Volviendo al menú.";
            case Key.ReconexOk: return "Reconexión correcta. Seguís en la partida.";
            case Key.ReconexionPerdida1v1: return "Se cortó la conexión. Perdiste la partida; gana el rival.";
            case Key.EspectandoAdmin: return "Modo espectador (admin).";
            case Key.GanastePartida: return "¡Ganaste la partida!";
            case Key.PerdistePartida: return "Perdiste la partida.";
            case Key.PerdisteMano: return "Perdiste la mano.";
            case Key.FaltaPanelCrear: return "No se pudo abrir el panel de crear (error interno).";
            case Key.ValidandoContrasena: return "Validando contraseña…";
            case Key.ContrasenaInvalida: return "Ingresá una contraseña válida.";
            case Key.ContrasenaIncorrecta: return "Contraseña incorrecta. Probá de nuevo.";
            case Key.EntryPrizeFormat: return "Entrada: {0}  →  Premio: {1}";
            case Key.CodigoMinPlaceholder: return "mín. 4 caracteres";
            case Key.PleaseWait: return "Esperá…";
            case Key.PhotonCreateFailed: return "No se pudo crear la sala en Photon: {0}";
            case Key.PhotonSyncWarning: return "No se pudo sincronizar el nombre de la sala con el servidor, pero se puede jugar. El admin puede no ver el nombre todavía.";
            case Key.PhotonConnectFailed: return "No se pudo conectar a Photon.";
            case Key.ChampionCongrats: return "¡Felicitaciones, sos el campeón!";
            case Key.LangEng: return "ENG";
            case Key.LangSpn: return "SPN";
            case Key.ModoJuego: return "Modo de juego";
            case Key.ConFlor: return "Con Flor";
            case Key.SinFlor: return "Sin Flor";
            case Key.ConfirmLeaveLobbyTitle: return "¿Salir de la sala?";
            case Key.ConfirmLeaveLobbyBody: return "Si salís ahora, perderás la sala y se reembolsará la entrada.";
            case Key.ConfirmNoQuedarme: return "NO";
            case Key.ConfirmSiSalir: return "SÍ, SALIR";
            case Key.SalaCanceladaReembolso: return "Sala cancelada. Entrada reembolsada.";
            case Key.NotificacionesTitulo: return "Notificaciones";
            case Key.NotificacionesVacio: return "No hay eventos todavía.";
            case Key.LogExito: return "Éxito";
            case Key.LogPendiente: return "Pendiente";
            case Key.LogAviso: return "Aviso";
            case Key.LogInfo: return "Info";
            case Key.LogSalaCreada: return "Sala creada. Esperando rival…";
            case Key.LogUnidoSala: return "Te uniste a una sala. Esperando inicio…";
            case Key.LogSalasActualizadas: return "Lista de salas actualizada ({0} disponibles).";
            default: return k.ToString();
        }
    }

    // ───────────────────────── English ─────────────────────────
    static string En(Key k)
    {
        switch (k)
        {
            case Key.ImportantTitle: return "IMPORTANT";
            case Key.Ok: return "OK";
            case Key.Espere: return "Please wait…";
            case Key.Partida1v1: return "1 vs 1 Match";
            case Key.SalasDisponibles: return "Available rooms";
            case Key.SeleccionarMesa: return "Select table";
            case Key.EntradaAbrev: return "Entry";
            case Key.PremioAbrev: return "Prize";
            case Key.JugadoresEnSala: return "Players: {0}/{1}";
            case Key.TuSalaEsperando: return "Your room";
            case Key.SalaLlena: return "Full";
            case Key.SalaExpiradaEtiqueta: return "Expired";
            case Key.SalaExpiradaAviso: return "This room expired. Create or join another room.";
            case Key.CrearSala: return "Create room";
            case Key.ActualizarLista: return "Refresh";
            case Key.Volver: return "Back";
            case Key.BuscandoOponente: return "Looking for opponent…";
            case Key.EsperandoRivalSala: return "Waiting for another player (2/2 to start)…";
            case Key.PartidaEncontrada: return "Opponent ready!";
            case Key.CargandoJuego: return "Loading match…";
            case Key.NombreSala: return "Room name";
            case Key.TipoSala: return "Room type";
            case Key.Publica: return "Public";
            case Key.Privada: return "Private";
            case Key.ContrasenaSala: return "Password";
            case Key.CodigoSala4: return "Room code (min. 4 characters)";
            case Key.CodigoPrivadaInfo: return "Private room: choose a code (min. 4 chars). Only you see it in the list.";
            case Key.CodigoInvalido4: return "Enter at least 4 characters (room password).";
            case Key.CodigoEtiqueta: return "Code";
            case Key.TuCodigoSala: return "Your code";
            case Key.CodigoParaUnir: return "Code";
            case Key.EntradaMonedas: return "Entry (coins)";
            case Key.Unirse: return "Join";
            case Key.Entrar: return "JOIN";
            case Key.SaldoInsuficiente: return "Insufficient balance for entry. Check the store or top up.";
            case Key.ErrorCrearSala: return "Could not create the room. Try again.";
            case Key.ErrorUnirse: return "Could not join the room.";
            case Key.Conectando: return "Connecting to match server…";
            case Key.IngresaContrasena: return "Enter the room password.";
            case Key.TiempoEsgotadoJugada: return "Time is up. Playing the leftmost card automatically.";
            case Key.CuentaRegresiva: return "Time: {0} s";
            case Key.TuTurno: return "Your turn";
            case Key.TurnoRival: return "Opponent's turn";
            case Key.Responde: return "Respond";
            case Key.EsperandoRespuestaRival: return "Waiting for opponent's response…";
            case Key.EsperandoJugadaRival: return "Waiting for opponent's play…";
            case Key.TiempoRespuestaAgotado: return "Response time expired. The call is declined automatically.";
            case Key.GanastePremio: return "You won {0} Trucoins prize!";
            case Key.GanaPorAbandono: return "You win: opponent left the match.";
            case Key.RivalReconectando: return "Opponent disconnected. Reconnecting…";
            case Key.Reconectando: return "Reconnecting…";
            case Key.ReconectandoOverlay: return "Reconnecting…";
            case Key.ReconectarFallo: return "Could not reconnect. Returning to menu.";
            case Key.ReconexOk: return "Reconnected. You are back in the match.";
            case Key.ReconexionPerdida1v1: return "Connection lost. You lose the match; opponent wins.";
            case Key.EspectandoAdmin: return "Spectator mode (admin).";
            case Key.GanastePartida: return "You won the match!";
            case Key.PerdistePartida: return "You lost the match.";
            case Key.PerdisteMano: return "You lost the hand.";
            case Key.FaltaPanelCrear: return "Could not open create panel (internal error).";
            case Key.ValidandoContrasena: return "Validating password…";
            case Key.ContrasenaInvalida: return "Enter a valid password.";
            case Key.ContrasenaIncorrecta: return "Wrong password. Try again.";
            case Key.EntryPrizeFormat: return "Entry: {0}  →  Prize: {1}";
            case Key.CodigoMinPlaceholder: return "min. 4 characters";
            case Key.PleaseWait: return "Please wait…";
            case Key.PhotonCreateFailed: return "Could not create the Photon room: {0}";
            case Key.PhotonSyncWarning: return "Could not sync the room name with the server, but you can still play. Admin may not see the name yet.";
            case Key.PhotonConnectFailed: return "Could not connect to Photon.";
            case Key.ChampionCongrats: return "Congratulations, you are the champion!";
            case Key.LangEng: return "ENG";
            case Key.LangSpn: return "SPN";
            case Key.ModoJuego: return "Game mode";
            case Key.ConFlor: return "w/ Flor";
            case Key.SinFlor: return "No Flor";
            case Key.ConfirmLeaveLobbyTitle: return "Leave the room?";
            case Key.ConfirmLeaveLobbyBody: return "If you leave now, you will lose the room and your entry will be refunded.";
            case Key.ConfirmNoQuedarme: return "NO";
            case Key.ConfirmSiSalir: return "YES, LEAVE";
            case Key.SalaCanceladaReembolso: return "Room cancelled. Entry refunded.";
            case Key.NotificacionesTitulo: return "Notifications";
            case Key.NotificacionesVacio: return "No events yet.";
            case Key.LogExito: return "Success";
            case Key.LogPendiente: return "Pending";
            case Key.LogAviso: return "Warning";
            case Key.LogInfo: return "Info";
            case Key.LogSalaCreada: return "Room created. Waiting for opponent…";
            case Key.LogUnidoSala: return "You joined a room. Waiting to start…";
            case Key.LogSalasActualizadas: return "Room list updated ({0} available).";
            default: return k.ToString();
        }
    }

    public enum Key
    {
        ImportantTitle, Ok, Espere,
        Partida1v1, SalasDisponibles, SeleccionarMesa, EntradaAbrev, PremioAbrev, JugadoresEnSala,
        TuSalaEsperando, SalaLlena, SalaExpiradaEtiqueta, SalaExpiradaAviso,
        CrearSala, ActualizarLista, Volver, BuscandoOponente, EsperandoRivalSala, PartidaEncontrada, CargandoJuego,
        NombreSala, TipoSala, Publica, Privada, ContrasenaSala, CodigoSala4, CodigoPrivadaInfo, CodigoInvalido4,
        CodigoEtiqueta, TuCodigoSala, CodigoParaUnir, EntradaMonedas, Unirse, Entrar,
        SaldoInsuficiente, ErrorCrearSala, ErrorUnirse, Conectando, IngresaContrasena,
        TiempoEsgotadoJugada, CuentaRegresiva, TuTurno, TurnoRival, Responde,
        EsperandoRespuestaRival, EsperandoJugadaRival, TiempoRespuestaAgotado,
        GanastePremio, GanaPorAbandono, RivalReconectando, Reconectando, ReconectandoOverlay,
        ReconectarFallo, ReconexOk, ReconexionPerdida1v1, EspectandoAdmin,
        GanastePartida, PerdistePartida, PerdisteMano, FaltaPanelCrear,
        ValidandoContrasena, ContrasenaInvalida, ContrasenaIncorrecta, EntryPrizeFormat,
        CodigoMinPlaceholder, PleaseWait, PhotonCreateFailed, PhotonSyncWarning, PhotonConnectFailed, ChampionCongrats,
        LangEng, LangSpn,
        ModoJuego, ConFlor, SinFlor,
        ConfirmLeaveLobbyTitle, ConfirmLeaveLobbyBody, ConfirmNoQuedarme, ConfirmSiSalir, SalaCanceladaReembolso,
        NotificacionesTitulo, NotificacionesVacio, LogExito, LogPendiente, LogAviso, LogInfo,
        LogSalaCreada, LogUnidoSala, LogSalasActualizadas
    }
}
