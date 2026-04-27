/// <summary>Spanish phrases for TTS (Río de la Plata / Paraguay friendly short forms).</summary>
public static class TrucoCalloutPhrases
{
    public static string ForEventCode(byte code)
    {
        if (code == UIMANAGER.TRUCO_CHALLENGE) return "Truco";
        if (code == UIMANAGER.RETRUCO_CHALLENGE) return "Re truco";
        if (code == UIMANAGER.VALE4_CHALLENGE) return "Vale cuatro";
        if (code == UIMANAGER.ENVIDO_CHALLENGE) return "Envido";
        if (code == UIMANAGER.REALENVIDO_CHALLENGE) return "Real envido";
        if (code == UIMANAGER.FALTAENVIDO_CHALLENGE) return "Falta envido";
        if (code == UIMANAGER.QUEIRO_CHALLENGE) return "Quiero";
        if (code == UIMANAGER.NOQUEIRO_CHALLENGE) return "No quiero";
        if (code == UIMANAGER.FLOR_CHALLENGE) return "Flor";
        if (code == UIMANAGER.FLOR_CHICA_CHALLENGE) return "Flor chica";
        if (code == UIMANAGER.CON_FLOR_QUIERO_CHALLENGE) return "Con flor quiero";
        if (code == UIMANAGER.CONTRA_FLOR_CHALLENGE) return "Contraflor";
        if (code == UIMANAGER.MAZO_CHALLENGE) return "Mazo";
        return null;
    }
}
