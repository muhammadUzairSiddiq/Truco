/// <summary>
/// UI copy for 1v1 / gameplay (Spanish default, English via <see cref="TrucoLocalization"/>).
/// </summary>
public static class TrucoTextosClient
{
    public static string ImportantTitle => TrucoLocalization.T(TrucoLocalization.Key.ImportantTitle);
    public static string Ok => TrucoLocalization.T(TrucoLocalization.Key.Ok);
    public static string Espere => TrucoLocalization.T(TrucoLocalization.Key.Espere);

    public static string Partida1v1 => TrucoLocalization.T(TrucoLocalization.Key.Partida1v1);
    public static string SalasDisponibles => TrucoLocalization.T(TrucoLocalization.Key.SalasDisponibles);
    public static string SeleccionarMesa => TrucoLocalization.T(TrucoLocalization.Key.SeleccionarMesa);
    public static string EntradaAbrev => TrucoLocalization.T(TrucoLocalization.Key.EntradaAbrev);
    public static string PremioAbrev => TrucoLocalization.T(TrucoLocalization.Key.PremioAbrev);
    public static string JugadoresEnSala => TrucoLocalization.T(TrucoLocalization.Key.JugadoresEnSala);
    public static string TuSalaEsperando => TrucoLocalization.T(TrucoLocalization.Key.TuSalaEsperando);
    public static string SalaLlena => TrucoLocalization.T(TrucoLocalization.Key.SalaLlena);
    public static string SalaExpiradaEtiqueta => TrucoLocalization.T(TrucoLocalization.Key.SalaExpiradaEtiqueta);
    public static string SalaExpiradaAviso => TrucoLocalization.T(TrucoLocalization.Key.SalaExpiradaAviso);
    public static string CrearSala => TrucoLocalization.T(TrucoLocalization.Key.CrearSala);
    public static string ActualizarLista => TrucoLocalization.T(TrucoLocalization.Key.ActualizarLista);
    public static string Volver => TrucoLocalization.T(TrucoLocalization.Key.Volver);
    public static string BuscandoOponente => TrucoLocalization.T(TrucoLocalization.Key.BuscandoOponente);
    public static string EsperandoRivalSala => TrucoLocalization.T(TrucoLocalization.Key.EsperandoRivalSala);
    public static string PartidaEncontrada => TrucoLocalization.T(TrucoLocalization.Key.PartidaEncontrada);
    public static string CargandoJuego => TrucoLocalization.T(TrucoLocalization.Key.CargandoJuego);

    public static string NombreSala => TrucoLocalization.T(TrucoLocalization.Key.NombreSala);
    public static string TipoSala => TrucoLocalization.T(TrucoLocalization.Key.TipoSala);
    public static string Publica => TrucoLocalization.T(TrucoLocalization.Key.Publica);
    public static string ModoJuego => TrucoLocalization.T(TrucoLocalization.Key.ModoJuego);
    public static string ConFlor => TrucoLocalization.T(TrucoLocalization.Key.ConFlor);
    public static string SinFlor => TrucoLocalization.T(TrucoLocalization.Key.SinFlor);
    public static string Privada => TrucoLocalization.T(TrucoLocalization.Key.Privada);
    public static string ContrasenaSala => TrucoLocalization.T(TrucoLocalization.Key.ContrasenaSala);
    public static string CodigoSala4 => TrucoLocalization.T(TrucoLocalization.Key.CodigoSala4);
    public static string CodigoPrivadaInfo => TrucoLocalization.T(TrucoLocalization.Key.CodigoPrivadaInfo);
    public static string CodigoInvalido4 => TrucoLocalization.T(TrucoLocalization.Key.CodigoInvalido4);
    public static string CodigoEtiqueta => TrucoLocalization.T(TrucoLocalization.Key.CodigoEtiqueta);
    public static string TuCodigoSala => TrucoLocalization.T(TrucoLocalization.Key.TuCodigoSala);
    public static string CodigoParaUnir => TrucoLocalization.T(TrucoLocalization.Key.CodigoParaUnir);
    public static string EntradaMonedas => TrucoLocalization.T(TrucoLocalization.Key.EntradaMonedas);

    public static string Unirse => TrucoLocalization.T(TrucoLocalization.Key.Unirse);
    public static string Entrar => TrucoLocalization.T(TrucoLocalization.Key.Entrar);
    public static string SaldoInsuficiente => TrucoLocalization.T(TrucoLocalization.Key.SaldoInsuficiente);
    public static string ErrorCrearSala => TrucoLocalization.T(TrucoLocalization.Key.ErrorCrearSala);
    public static string ErrorUnirse => TrucoLocalization.T(TrucoLocalization.Key.ErrorUnirse);
    public static string Conectando => TrucoLocalization.T(TrucoLocalization.Key.Conectando);
    public static string IngresaContrasena => TrucoLocalization.T(TrucoLocalization.Key.IngresaContrasena);

    public static string TiempoEsgotadoJugada => TrucoLocalization.T(TrucoLocalization.Key.TiempoEsgotadoJugada);
    public static string CuentaRegresiva => TrucoLocalization.T(TrucoLocalization.Key.CuentaRegresiva);
    public static string TuTurno => TrucoLocalization.T(TrucoLocalization.Key.TuTurno);
    public static string TurnoRival => TrucoLocalization.T(TrucoLocalization.Key.TurnoRival);

    public const int TurnoTimerUrgenteHastaSegundos = 5;

    public static string FormatoBannerTuTurnoConSegundos(int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TurnoTimerUrgenteHastaSegundos;
        string cLabel = urgent ? "#f0dcc0" : "#c8d4e0";
        string cNum = urgent ? "#ffc24a" : "#ffffff";
        string cSuf = urgent ? "#e8a060" : "#aeb8c4";
        string szNum = urgent ? "86" : "80";
        return "<align=center><line-height=76%><size=34><color=" + cLabel + ">" + TuTurno
            + "</color></size><br><size=" + szNum + "><color=" + cNum + "><b>" + sec
            + "</b></color></size><size=30><color=" + cSuf + "> s</color></size></line-height></align>";
    }

    public static string FormatoBannerTurnoRivalConSegundos(int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TurnoTimerUrgenteHastaSegundos;
        string cLabel = urgent ? "#f0dcc0" : "#c8d4e0";
        string cNum = urgent ? "#ffc24a" : "#ffffff";
        string cSuf = urgent ? "#e8a060" : "#aeb8c4";
        string szNum = urgent ? "86" : "80";
        return "<align=center><line-height=76%><size=34><color=" + cLabel + ">" + TurnoRival
            + "</color></size><br><size=" + szNum + "><color=" + cNum + "><b>" + sec
            + "</b></color></size><size=30><color=" + cSuf + "> s</color></size></line-height></align>";
    }

    public static string FormatoBannerResponderConSegundos(int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TurnoTimerUrgenteHastaSegundos;
        string cLabel = urgent ? "#f0dcc0" : "#ffe2b0";
        string cNum = urgent ? "#ffc24a" : "#ffd166";
        string cSuf = urgent ? "#e8a060" : "#c9a96a";
        string szNum = urgent ? "86" : "80";
        return "<align=center><line-height=76%><size=34><color=" + cLabel + ">" + Responde
            + "</color></size><br><size=" + szNum + "><color=" + cNum + "><b>" + sec
            + "</b></color></size><size=30><color=" + cSuf + "> s</color></size></line-height></align>";
    }

    public static string Responde => TrucoLocalization.T(TrucoLocalization.Key.Responde);
    public static string EsperandoRespuestaRival => TrucoLocalization.T(TrucoLocalization.Key.EsperandoRespuestaRival);
    public static string EsperandoJugadaRival => TrucoLocalization.T(TrucoLocalization.Key.EsperandoJugadaRival);
    public static string TiempoRespuestaAgotado => TrucoLocalization.T(TrucoLocalization.Key.TiempoRespuestaAgotado);

    public static string GanastePremio => TrucoLocalization.T(TrucoLocalization.Key.GanastePremio);
    public static string GanaPorAbandono => TrucoLocalization.T(TrucoLocalization.Key.GanaPorAbandono);
    public static string RivalReconectando => TrucoLocalization.T(TrucoLocalization.Key.RivalReconectando);
    public static string Reconectando => TrucoLocalization.T(TrucoLocalization.Key.Reconectando);
    public static string ReconectandoOverlay => TrucoLocalization.T(TrucoLocalization.Key.ReconectandoOverlay);
    public static string ReconectarFallo => TrucoLocalization.T(TrucoLocalization.Key.ReconectarFallo);
    public static string ReconexOk => TrucoLocalization.T(TrucoLocalization.Key.ReconexOk);
    public static string ReconexionPerdida1v1 => TrucoLocalization.T(TrucoLocalization.Key.ReconexionPerdida1v1);
    public static string EspectandoAdmin => TrucoLocalization.T(TrucoLocalization.Key.EspectandoAdmin);

    public static string GanastePartida => TrucoLocalization.T(TrucoLocalization.Key.GanastePartida);
    public static string PerdistePartida => TrucoLocalization.T(TrucoLocalization.Key.PerdistePartida);
    public static string PerdisteMano => TrucoLocalization.T(TrucoLocalization.Key.PerdisteMano);
    public static string FaltaPanelCrear => TrucoLocalization.T(TrucoLocalization.Key.FaltaPanelCrear);
    public static string ValidandoContrasena => TrucoLocalization.T(TrucoLocalization.Key.ValidandoContrasena);
    public static string ContrasenaInvalida => TrucoLocalization.T(TrucoLocalization.Key.ContrasenaInvalida);
    public static string ContrasenaIncorrecta => TrucoLocalization.T(TrucoLocalization.Key.ContrasenaIncorrecta);
    public static string EntryPrizeFormat => TrucoLocalization.T(TrucoLocalization.Key.EntryPrizeFormat);
    public static string PleaseWait => TrucoLocalization.T(TrucoLocalization.Key.PleaseWait);
    public static string PhotonCreateFailed => TrucoLocalization.T(TrucoLocalization.Key.PhotonCreateFailed);
    public static string PhotonSyncWarning => TrucoLocalization.T(TrucoLocalization.Key.PhotonSyncWarning);
    public static string PhotonConnectFailed => TrucoLocalization.T(TrucoLocalization.Key.PhotonConnectFailed);
    public static string ChampionCongrats => TrucoLocalization.T(TrucoLocalization.Key.ChampionCongrats);

    public static string ConfirmLeaveLobbyTitle => TrucoLocalization.T(TrucoLocalization.Key.ConfirmLeaveLobbyTitle);
    public static string ConfirmLeaveLobbyBody => TrucoLocalization.T(TrucoLocalization.Key.ConfirmLeaveLobbyBody);
    public static string ConfirmNoQuedarme => TrucoLocalization.T(TrucoLocalization.Key.ConfirmNoQuedarme);
    public static string ConfirmSiSalir => TrucoLocalization.T(TrucoLocalization.Key.ConfirmSiSalir);
    public static string SalaCanceladaReembolso => TrucoLocalization.T(TrucoLocalization.Key.SalaCanceladaReembolso);
    public static string NotificacionesTitulo => TrucoLocalization.T(TrucoLocalization.Key.NotificacionesTitulo);
    public static string NotificacionesVacio => TrucoLocalization.T(TrucoLocalization.Key.NotificacionesVacio);
    public static string LogExito => TrucoLocalization.T(TrucoLocalization.Key.LogExito);
    public static string LogPendiente => TrucoLocalization.T(TrucoLocalization.Key.LogPendiente);
    public static string LogAviso => TrucoLocalization.T(TrucoLocalization.Key.LogAviso);
    public static string LogInfo => TrucoLocalization.T(TrucoLocalization.Key.LogInfo);
    public static string LogSalaCreada => TrucoLocalization.T(TrucoLocalization.Key.LogSalaCreada);
    public static string LogUnidoSala => TrucoLocalization.T(TrucoLocalization.Key.LogUnidoSala);
    public static string LogSalasActualizadas => TrucoLocalization.T(TrucoLocalization.Key.LogSalasActualizadas);

    // Admin / live dashboard (English-only labels kept for admin tooling)
    public const string LiveDashboardTitle = "Panel en vivo — partidas 1v1";
    public const string LiveDashboardButton = "Live Dashboard";
    public const string AdminVolver = "Volver";
    public const string AdminRefreshList = "Actualizar";
    public const string AdminPhotonRoomLabel = "Sala Photon:";
    public const string LiveRowMetaTemplate = "Estado: {0}  ·  {1}  ·  Entrada: {2}  ·  Jugadores: {3}/2";
    public const string SpectateEntrando = "Entrando como espectador…";
    public const string LiveDashboardFraud = "Resumen de alertas";
    public const string Spectate = "Ver / espectar";
    public const string ForceCloseMatch = "Cerrar partida (admin)";
    public const string VolverMenuAdmin = "Volver al menú de admin";
}
