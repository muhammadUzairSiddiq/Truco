using NUnit.Framework;

public class TrucoClientSettingsEditModeTests
{
    [TearDown]
    public void TearDown()
    {
        TrucoClientSettings.ApplyAsset(null);
    }

    [Test]
    public void ResolveLanguage_OnlySpanish()
    {
        var so = UnityEngine.ScriptableObject.CreateInstance<TrucoClientSettingsSO>();
        so.spanishEnabled = true;
        so.englishEnabled = false;
        TrucoClientSettings.ApplyAsset(so);
        Assert.AreEqual(TrucoLocalization.Lang.Spanish,
            TrucoClientSettings.ResolveLanguage(TrucoLocalization.Lang.English));
    }

    [Test]
    public void ResolveLanguage_OnlyEnglish()
    {
        var so = UnityEngine.ScriptableObject.CreateInstance<TrucoClientSettingsSO>();
        so.spanishEnabled = false;
        so.englishEnabled = true;
        TrucoClientSettings.ApplyAsset(so);
        Assert.AreEqual(TrucoLocalization.Lang.English,
            TrucoClientSettings.ResolveLanguage(TrucoLocalization.Lang.Spanish));
    }

    [Test]
    public void PhotonJoinMaxAttempts_EightSecondsTotal()
    {
        var so = UnityEngine.ScriptableObject.CreateInstance<TrucoClientSettingsSO>();
        so.photonJoinRetryTotalSeconds = 8f;
        so.photonJoinRetryIntervalSeconds = 1f;
        TrucoClientSettings.ApplyAsset(so);
        Assert.AreEqual(8, TrucoClientSettings.PhotonJoinMaxAttempts);
    }

    [Test]
    public void ShowLanguageToggle_RequiresBothEnabled()
    {
        var so = UnityEngine.ScriptableObject.CreateInstance<TrucoClientSettingsSO>();
        so.spanishEnabled = true;
        so.englishEnabled = true;
        so.showLanguageToggleInMenu = true;
        TrucoClientSettings.ApplyAsset(so);
        Assert.IsTrue(TrucoClientSettings.ShowLanguageToggleInMenu);

        so.englishEnabled = false;
        TrucoClientSettings.ApplyAsset(so);
        Assert.IsFalse(TrucoClientSettings.ShowLanguageToggleInMenu);
    }
}
