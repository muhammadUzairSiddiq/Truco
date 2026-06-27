using UnityEngine;

/// <summary>Bold white countdown copy on dark panels — used by turn, canto, and reconnect timers.</summary>
public static class TrucoGameplayTimerBanner
{
    public static readonly Color BgNormal = new Color(0f, 0f, 0f, 0.84f);
    public static readonly Color BgUrgent = new Color(0.14f, 0.02f, 0.02f, 0.92f);
    public static readonly Color BgReconnect = new Color(0f, 0f, 0f, 0.9f);

    public static string ConSegundos(string label, int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
        int numSize = urgent ? 132 : 116;
        return "<align=center><line-height=80%>"
               + "<size=40><color=#FFFFFF><b>" + label + "</b></color></size><br>"
               + "<size=" + numSize + "><color=#FFFFFF><b>" + sec + "</b></color></size>"
               + "<size=38><color=#FFFFFF><b> s</b></color></size>"
               + "</line-height></align>";
    }

    public static string SoloMensaje(string msg) =>
        "<align=center><size=42><color=#FFFFFF><b>" + msg + "</b></color></size></align>";

    public static string OverlayTituloYSegundos(string title, int sec)
    {
        if (sec < 0) sec = 0;
        return "<align=center><line-height=82%>"
               + "<size=44><color=#FFFFFF><b>" + title + "</b></color></size><br>"
               + "<size=150><color=#FFFFFF><b>" + sec + "</b></color></size>"
               + "<size=40><color=#FFFFFF><b> s</b></color></size>"
               + "</line-height></align>";
    }
}
