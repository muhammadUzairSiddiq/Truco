using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

public sealed class TrucoHttpSessionStateTests
{
    static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void PartialAccessAndXsrfUpdatesPreserveRefreshCookie()
    {
        var jar = new TrucoSessionCookieJar();
        jar.Merge("access_token=first; Path=/, refresh_token=refresh; HttpOnly, XSRF-TOKEN=old", Now);
        jar.Merge("access_token=second; Path=/", Now);
        jar.Merge("XSRF-TOKEN=new; Secure", Now);
        Assert.AreEqual("second", jar.Value("access_token"));
        Assert.AreEqual("refresh", jar.Value("refresh_token"));
        Assert.AreEqual("new", jar.Value("XSRF-TOKEN"));
        StringAssert.Contains("refresh_token=refresh", jar.Header);
        StringAssert.DoesNotContain("HttpOnly", jar.Header);
    }

    [Test]
    public void CombinedExpiresCommasAndEqualsInsideValuesArePreserved()
    {
        var jar = new TrucoSessionCookieJar();
        string future = Now.AddDays(5).ToString("r", CultureInfo.InvariantCulture);
        jar.Merge("refresh_token=refresh==; Expires=" + future + ", access_token=access=value==; Path=/api", Now);
        Assert.AreEqual("refresh==", jar.Value("refresh_token"));
        Assert.AreEqual("access=value==", jar.Value("access_token"));
        jar.Prune(Now.AddDays(6));
        Assert.IsNull(jar.Value("refresh_token"));
        Assert.AreEqual("access=value==", jar.Value("access_token"));
    }

    [Test]
    public void RepeatedHeaderLinesAndQuotedCommaDoNotCreateInventedCookies()
    {
        var jar = new TrucoSessionCookieJar();
        jar.Merge("opaque=\"one,two=three\"; Path=/\r\nrefresh_token=refresh\nXSRF-TOKEN=xsrf", Now);
        Assert.AreEqual("\"one,two=three\"", jar.Value("opaque"));
        Assert.IsNull(jar.Value("two"));
        Assert.AreEqual("refresh", jar.Value("refresh_token"));
        Assert.AreEqual("xsrf", jar.Value("XSRF-TOKEN"));
    }

    [TestCase("Max-Age=0")]
    [TestCase("Max-Age=-3")]
    [TestCase("Expires=Thu, 01 Jan 1970 00:00:00 GMT")]
    public void ServerDeletionRemovesOnlyTheNamedCookie(string attribute)
    {
        var jar = new TrucoSessionCookieJar();
        jar.Merge("access_token=access, refresh_token=refresh, XSRF-TOKEN=xsrf", Now);
        jar.Merge("refresh_token=; " + attribute, Now);
        Assert.IsNull(jar.Value("refresh_token"));
        Assert.AreEqual("access", jar.Value("access_token"));
        Assert.AreEqual("xsrf", jar.Value("XSRF-TOKEN"));
        StringAssert.DoesNotContain("refresh_token=", jar.Header);
    }

    [Test]
    public void MaxAgeTakesPrecedenceOverExpiresAndExpiresWithoutAnotherResponse()
    {
        var jar = new TrucoSessionCookieJar();
        jar.Merge("refresh_token=refresh; Max-Age=60; Expires=Thu, 01 Jan 1970 00:00:00 GMT", Now);
        Assert.AreEqual("refresh", jar.Value("refresh_token"));
        Assert.IsEmpty(jar.Prune(Now.AddSeconds(59)));
        CollectionAssert.Contains(jar.Prune(Now.AddSeconds(60)), "refresh_token");
        Assert.AreEqual("", jar.Header);
    }

    [Test]
    public void SessionCookieWithoutExpirySurvivesTimeAndClearRemovesEverything()
    {
        var jar = new TrucoSessionCookieJar();
        jar.Merge("refresh_token=refresh; HttpOnly", Now);
        jar.Prune(Now.AddYears(20)); Assert.AreEqual("refresh", jar.Value("refresh_token"));
        jar.Clear(); Assert.IsNull(jar.Value("refresh_token")); Assert.AreEqual("", jar.Header);
    }

    [Test]
    public void HttpFieldsAndHeaderStayInSyncAcrossPartialUpdatesAndLogout()
    {
        HttpApiClient.ClearAuthSession();
        try
        {
            Merge("access_token=first, refresh_token=refresh, XSRF-TOKEN=xsrf");
            Merge("access_token=second"); Merge("XSRF-TOKEN=new");
            Assert.AreEqual("second", Field("_accessToken")); Assert.AreEqual("refresh", Field("_refreshToken"));
            StringAssert.Contains("refresh_token=refresh", (string)Field("_cookieHeader"));
            Merge("refresh_token=; Max-Age=0");
            Assert.IsNull(Field("_refreshToken")); Assert.AreEqual("second", Field("_accessToken"));
            int generation = HttpApiClient.SessionGeneration;
            HttpApiClient.ClearAuthSession();
            Assert.Greater(HttpApiClient.SessionGeneration, generation);
            foreach (string field in new[] { "_cookieHeader", "_xsrfToken", "_accessToken", "_refreshToken" }) Assert.IsNull(Field(field));
            Merge("XSRF-TOKEN=next-session");
            StringAssert.DoesNotContain("access_token", (string)Field("_cookieHeader"));
        }
        finally { HttpApiClient.ClearAuthSession(); }
    }

    [Test]
    public void UnrelatedCookieCannotEraseManuallySuppliedBearerAndExpiryClearsTrackedToken()
    {
        HttpApiClient.ClearAuthSession();
        try
        {
            typeof(HttpApiClient).GetMethod("UpdateAccessToken", PrivateStatic).Invoke(null, new object[] { "body-bearer" });
            Merge("XSRF-TOKEN=xsrf"); Assert.AreEqual("body-bearer", Field("_accessToken"));
            Merge("access_token=cookie-bearer; Max-Age=1");
            var jar = (TrucoSessionCookieJar)Field("Cookies");
            typeof(HttpApiClient).GetMethod("SynchronizeCookieState", PrivateStatic).Invoke(null, new object[] { jar.Prune(Now.AddSeconds(2)) });
            Assert.IsNull(Field("_accessToken"));
            Assert.AreEqual("xsrf", Field("_xsrfToken"));
        }
        finally { HttpApiClient.ClearAuthSession(); }
    }

    [Test]
    public async Task ConcurrentRefreshesShareOneNetworkOperationForTheGeneration()
    {
        var gate = new TrucoSessionRefreshGate(); var server = new TaskCompletionSource<bool>(); int calls = 0;
        Func<Task<bool>> refresh = () => { calls++; return server.Task; };
        Task<bool> first = gate.Run(7, refresh, () => 7);
        Task<bool> second = gate.Run(7, refresh, () => 7);
        Assert.AreSame(first, second); Assert.AreEqual(1, calls);
        server.SetResult(true); Assert.IsTrue(await first); Assert.IsTrue(await second);
    }

    [Test]
    public async Task LogoutRejectsOldRefreshAndItsCompletionCannotClearTheNewSessionRefresh()
    {
        HttpApiClient.ClearAuthSession();
        try
        {
            var gate = (TrucoSessionRefreshGate)Field("RefreshGate");
            var oldServer = new TaskCompletionSource<bool>(); var newServer = new TaskCompletionSource<bool>();
            Task<bool> old = gate.Run(HttpApiClient.SessionGeneration, () => oldServer.Task, () => HttpApiClient.SessionGeneration);
            HttpApiClient.ClearAuthSession(); Assert.IsFalse(await old);
            Task<bool> current = gate.Run(HttpApiClient.SessionGeneration, () => newServer.Task, () => HttpApiClient.SessionGeneration);
            oldServer.SetResult(true);
            Task<bool> joined = gate.Run(HttpApiClient.SessionGeneration, () => throw new Exception("Must not start a second refresh"), () => HttpApiClient.SessionGeneration);
            Assert.AreSame(current, joined);
            newServer.SetResult(true); Assert.IsTrue(await current); Assert.IsFalse(await old);
        }
        finally { HttpApiClient.ClearAuthSession(); }
    }

    [Test]
    public async Task ChangedGenerationCannotReturnSuccessfulRefreshEvenWithoutReset()
    {
        var gate = new TrucoSessionRefreshGate(); var server = new TaskCompletionSource<bool>(); int generation = 3;
        Task<bool> pending = gate.Run(generation, () => server.Task, () => generation);
        generation++; server.SetResult(true); Assert.IsFalse(await pending);
    }

    [Test]
    public async Task FailedRefreshIsSharedAndAllowsLaterExplicitRetry()
    {
        var gate = new TrucoSessionRefreshGate(); var server = new TaskCompletionSource<bool>(); int calls = 0;
        Func<Task<bool>> refresh = () => { calls++; return server.Task; };
        Task<bool> first = gate.Run(1, refresh, () => 1); Task<bool> second = gate.Run(1, refresh, () => 1);
        server.SetException(new InvalidOperationException("Synthetic refresh failure"));
        Assert.IsFalse(await first); Assert.IsFalse(await second); Assert.AreEqual(1, calls);
        Assert.IsTrue(await gate.Run(1, () => Task.FromResult(true), () => 1));
    }

    static object Field(string name) => typeof(HttpApiClient).GetField(name, PrivateStatic).GetValue(null);
    static void Merge(string header) => typeof(HttpApiClient).GetMethod("MergeSessionCookies", PrivateStatic).Invoke(null, new object[] { header, Now });
}
