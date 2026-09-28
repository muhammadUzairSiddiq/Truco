/// <summary>Single gate for tables / tournaments / wallet mutations during maintenance.</summary>
public static class TrucoPlayAccess
{
    public static bool TryTables()
    {
        if (!TrucoMaintenanceMode.BlocksTables) return true;
        AppManager.Instance?.DisplayNotification(TrucoMaintenanceMode.Message);
        return false;
    }

    public static bool TryTournaments()
    {
        if (!TrucoMaintenanceMode.BlocksTournaments) return true;
        AppManager.Instance?.DisplayNotification(TrucoMaintenanceMode.Message);
        return false;
    }

    public static bool TryTransactions()
    {
        if (!TrucoMaintenanceMode.BlocksTransactions) return true;
        AppManager.Instance?.DisplayNotification(TrucoMaintenanceMode.Message);
        return false;
    }
}
