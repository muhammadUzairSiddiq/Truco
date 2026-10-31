using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TrucoRemoteApiConfigurationTests
{
    static TrucoRemoteApiConfiguration.Payload Payload(string path = "/wallet/requests/{0}") => new TrucoRemoteApiConfiguration.Payload {
        schemaVersion = 1, minimumClientContractVersion = 1, revision = 2,
        apiBaseUrl = "https://api.example.test/v2",
        endpoints = new[] { new TrucoApiEndpointsSO.Endpoint { key = "WalletRequestStatus", method = "GET", pathTemplate = path } }
    };
    static TrucoApiEndpointsSO Settings()
    {
        var settings = ScriptableObject.CreateInstance<TrucoApiEndpointsSO>();
        settings.baseUrl = "https://api.example.test/api"; return settings;
    }
    [Test]
    public void CompatibleSameOriginPathUpdatesAreAcceptedWithoutChangingTheClient()
    {
        var settings = Settings();
        try { Assert.IsTrue(TrucoRemoteApiConfiguration.Validate(Payload(), settings, 1, out var error), error); }
        finally { Object.DestroyImmediate(settings); }
    }
    [TestCase("https://other.example.test/api")]
    [TestCase("http://api.example.test/api")]
    [TestCase("https://user:password@api.example.test/api")]
    [TestCase("https://api.example.test/api?secret=x")]
    public void RemoteUpdatesCannotMoveAuthenticatedCallsToAnUntrustedOrigin(string url)
    {
        var settings = Settings(); var payload = Payload(); payload.apiBaseUrl = url;
        try { Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(payload, settings, 1, out _)); }
        finally { Object.DestroyImmediate(settings); }
    }
    [TestCase("//evil.example.test")]
    [TestCase("/wallet/../admin")]
    [TestCase("/wallet/%2e%2e/admin")]
    [TestCase("/wallet\\admin")]
    [TestCase("https://evil.example.test")]
    [TestCase("/wallet/{unbound}")]
    [TestCase("/wallet/{0}/bad\nheader")]
    public void UnsafeTemplatesAreRejected(string path) => Assert.IsFalse(TrucoRemoteApiConfiguration.ValidPath(path));
    [Test]
    public void DuplicateKeysRollbackAndBreakingContractVersionsAreRejected()
    {
        var settings = Settings();
        try
        {
            var payload = Payload(); payload.endpoints = new[] { payload.endpoints[0], payload.endpoints[0] };
            Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(payload, settings, 1, out _));
            Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(Payload(), settings, 3, out _));
            payload = Payload(); payload.minimumClientContractVersion = 2;
            Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(payload, settings, 1, out _));
        }
        finally { Object.DestroyImmediate(settings); }
    }
    [Test]
    public void RemoteMetadataCannotChangeTheCompiledHttpMethod()
    {
        var settings = Settings();
        settings.endpoints.Add(new TrucoApiEndpointsSO.Endpoint { key = "WalletRequestStatus", method = "GET", pathTemplate = "/wallet/requests/{0}" });
        var payload = Payload(); payload.endpoints[0].method = "POST";
        try { Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(payload, settings, 1, out _)); }
        finally { Object.DestroyImmediate(settings); }
    }
    [Test]
    public void RemoteRoutesMustPreserveWhetherTheCallerSuppliesAnId()
    {
        var settings = Settings();
        settings.endpoints.Add(new TrucoApiEndpointsSO.Endpoint { key = "WalletRequestStatus", method = "GET", pathTemplate = "/wallet/requests/{0}" });
        try
        {
            Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(Payload("/wallet/current"), settings, 1, out _));
            settings.endpoints[0].pathTemplate = "/wallet/current";
            Assert.IsFalse(TrucoRemoteApiConfiguration.Validate(Payload(), settings, 1, out _));
        }
        finally { Object.DestroyImmediate(settings); }
    }
    [Test]
    public void AuthAdminAndLegacyRoutesUseTheSameSOOverridesAndEscapeIds()
    {
        var previousBase = ApiConfig.BaseUrl;
        var field = typeof(ApiConfig).GetField("_overrides", BindingFlags.NonPublic | BindingFlags.Static);
        var previousRoutes = field.GetValue(null); var settings = Settings();
        settings.endpoints = new List<TrucoApiEndpointsSO.Endpoint> {
            new TrucoApiEndpointsSO.Endpoint { key = "Login", pathTemplate = "/v2/session" },
            new TrucoApiEndpointsSO.Endpoint { key = "AdminListMatches", pathTemplate = "/v2/admin/tables" },
            new TrucoApiEndpointsSO.Endpoint { key = "GetMatch", pathTemplate = "/v2/table/{0}" }
        };
        try
        {
            ApiConfig.ApplyEndpoints(settings);
            Assert.AreEqual("https://api.example.test/api/v2/session", ApiConfig.Login);
            Assert.AreEqual("https://api.example.test/api/v2/admin/tables", ApiConfig.AdminListMatches);
            Assert.AreEqual("https://api.example.test/api/v2/table/a%2Fb", ApiConfig.GetMatch("a/b"));
        }
        finally { ApiConfig.BaseUrl = previousBase; field.SetValue(null, previousRoutes); Object.DestroyImmediate(settings); }
    }
}
