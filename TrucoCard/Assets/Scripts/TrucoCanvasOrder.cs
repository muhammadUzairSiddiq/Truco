/// <summary>
/// Unity stores Canvas sorting orders in a signed 16-bit value. Keep overlays below
/// 32767 so they remain visible and receive pointer events above the menu.
/// </summary>
public static class TrucoCanvasOrder
{
    public const int Navigation = 32000;
    public const int Screen = 32300;
    public const int Bracket = 32525;
    public const int TournamentStatus = 32500;
    public const int Champion = 32550;
    public const int EditorPractice = 32600;
    public const int Confirm = 32650;
    public const int Maintenance = 32675;
    public const int Reconnection = 32700;
    public const int Transition = 32760;
}
