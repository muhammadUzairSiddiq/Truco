/// <summary>
/// Textos de interfaz en español (estilo paraguayo / Río de la Plata, sin muletillas argentinas forzadas).
/// Ajustar copy con el cliente según reglas locales del Truco en Paraguay.
/// </summary>
public static class TrucoTextosClient
{
    public const string Partida1v1 = "Partida 1 vs 1";
    public const string SalasDisponibles = "Salas disponibles";
    public const string SeleccionarMesa = "Seleccionar mesa";
    public const string EntradaAbrev = "Entrada";
    public const string JugadoresEnSala = "Jugadores: {0}/{1}";
    public const string TuSalaEsperando = "Tu sala";
    /// <summary>English: room is full. Short label for join button.</summary>
    public const string SalaLlena = "Full";
    /// <summary>Short label under join button when API says full but Photon shows an empty room.</summary>
    public const string SalaExpiradaEtiqueta = "Expirado";
    /// <summary>Toast / IMPORTANT when user tries to join an expired row.</summary>
    public const string SalaExpiradaAviso = "La sala expiró. Creá o unite a otra sala.";
    public const string CrearSala = "Crear sala";
    public const string ActualizarLista = "Actualizar";
    public const string Volver = "Volver";
    public const string BuscandoOponente = "Buscando oponente…";
    public const string EsperandoRivalSala = "Esperando a otro jugador (2/2 para empezar)…";
    public const string PartidaEncontrada = "¡Oponente listo!";
    public const string CargandoJuego = "Cargando partida…";

    public const string NombreSala = "Nombre de la sala";
    public const string TipoSala = "Tipo de sala";
    public const string Publica = "Pública";
    public const string Privada = "Privada";
    public const string ContrasenaSala = "Contraseña";
    /// <summary>Must match server rule for <c>password</c> (min 6 characters).</summary>
    public const string CodigoSala4 = "Código de sala (mín. 6 caracteres)";
    public const string CodigoPrivadaInfo = "Sala privada: elegí un código (mín. 6 caracteres, igual que pide el servidor). Solo vos lo verás en la lista.";
    public const string CodigoInvalido4 = "Ingresá al menos 6 caracteres (contraseña de la sala).";
    public const string CodigoEtiqueta = "Cód.";
    public const string TuCodigoSala = "Tu código";
    public const string CodigoParaUnir = "Código";
    public const string EntradaMonedas = "Entrada (monedas)";

    public const string Unirse = "Unirse";
    /// <summary>Short CTA on list rows (match compact list UI).</summary>
    public const string Entrar = "ENTRAR";
    public const string SaldoInsuficiente = "No tenés saldo suficiente para la entrada. Revisá en la tienda o recargá.";
    public const string ErrorCrearSala = "No se pudo crear la sala. Intentá otra vez.";
    public const string ErrorUnirse = "No se pudo unir a la sala.";
    public const string Conectando = "Conectando al servidor de partida…";
    public const string IngresaContrasena = "Ingresá la contraseña de la sala.";

    public const string TiempoEsgotadoJugada = "Se acabó el tiempo. Se juega la carta automáticamente (regla local de espera).";
    public const string CuentaRegresiva = "Tiempo: {0} s";
    public const string TuTurno = "Tu turno";
    public const string TurnoRival = "Turno del rival";

    /// <summary>Desde 1s hasta este inclusive, el UI muestra aviso (color de urgencia).</summary>
    public const int TurnoTimerUrgenteHastaSegundos = 5;

    /// <summary>Formato de banner superior (TextMeshPro rich text) para el cronómetro de 30 s. Incluye color de aviso al quedar poco tiempo.</summary>
    public static string FormatoBannerTuTurnoConSegundos(int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TurnoTimerUrgenteHastaSegundos;
        // >5 s: gris-azul suave + dígitos blancos. ≤5 s: leve ámbar + dígitos ámbar/dorado (alta visibilidad).
        string cLabel = urgent ? "#f0dcc0" : "#c8d4e0";
        string cNum = urgent ? "#ffc24a" : "#ffffff";
        string cSuf = urgent ? "#e8a060" : "#aeb8c4";
        string szNum = urgent ? "86" : "80";
        return "<align=center><line-height=76%><size=34><color=" + cLabel + ">" + TuTurno
            + "</color></size><br><size=" + szNum + "><color=" + cNum + "><b>" + sec
            + "</b></color></size><size=30><color=" + cSuf + "> s</color></size></line-height></align>";
    }
    public const string GanaPorAbandono = "Ganaste: el rival dejó la partida.";
    public const string Reconectando = "Reconectando…";
    /// <summary>Overlay a pantalla completa (línea corta + número de segundos al lado).</summary>
    public const string ReconectandoOverlay = "Reconectando…";
    public const string ReconectarFallo = "No se pudo reconectar. Volviendo al menú.";
    public const string ReconexOk = "Reconexión correcta. Seguís en la partida.";
    public const string ReconexionPerdida1v1 = "Se cortó la conexión. Perdiste la partida; gana el rival.";
    public const string EspectandoAdmin = "Modo espectador (admin).";
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

    public const string GanastePartida = "¡Ganaste la partida!";
    public const string PerdistePartida = "Perdiste la partida.";
    public const string PerdisteMano = "Perdiste la mano.";
    public const string FaltaPanelCrear = "No se pudo abrir el panel de crear (error interno).";
}
