using UnityEngine;

/// <summary>
/// Compact status-banner copy for turn / canto / reconnect timers.
/// Sized to fit the gameplay TurnText rect without overlapping lines.
/// </summary>
public static class TrucoGameplayTimerBanner
{
    public static readonly Color BgNormal = new Color(0.05f, 0.07f, 0.1f, 0.88f);
    public static readonly Color BgUrgent = new Color(0.28f, 0.06f, 0.06f, 0.92f);
    public static readonly Color BgReconnect = new Color(0.02f, 0.02f, 0.04f, 0.94f);

    public static string ConSegundos(string label, int sec)
    {
        if (sec < 0) sec = 0;
        bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
        string numColor = urgent ? "#FFB4B4" : "#FFFFFF";
        return "<align=center><line-height=88%>"
               + "<size=22><color=#D8DEE8>" + Escape(label) + "</color></size><br>"
               + "<size=40><color=" + numColor + "><b>" + sec + "</b></color></size>"
               + "<size=18><color=#A8B0BC> s</color></size>"
               + "</line-height></align>";
    }

    /// <summary>Waiting message + countdown (challenger watching opponent respond).</summary>
    public static string EsperandoConSegundos(string msg, int sec)
    {
        if (sec < 0) sec = 0;
        return "<align=center><line-height=88%>"
               + "<size=20><color=#D8DEE8>" + Escape(msg) + "</color></size><br>"
               + "<size=36><color=#FFFFFF><b>" + sec + "</b></color></size>"
               + "<size=16><color=#A8B0BC> s</color></size>"
               + "</line-height></align>";
    }

    public static string SoloMensaje(string msg) =>
        "<align=center><size=26><color=#FFFFFF><b>" + Escape(msg) + "</b></color></size></align>";

    public static string OverlayTituloYSegundos(string title, int sec)
    {
        if (sec < 0) sec = 0;
        return "<align=center><line-height=86%>"
               + "<size=28><color=#FFFFFF><b>" + Escape(title) + "</b></color></size><br>"
               + "<size=72><color=#FFFFFF><b>" + sec + "</b></color></size>"
               + "<size=22><color=#A8B0BC> s</color></size>"
               + "</line-height></align>";
    }

    static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("<", "\u2039").Replace(">", "\u203A");
    }
}
