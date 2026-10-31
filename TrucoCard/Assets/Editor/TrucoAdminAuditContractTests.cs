using System;
using NUnit.Framework;

public sealed class TrucoAdminAuditContractTests
{
    [Test]
    public void ServerAuditPreservesIdentityTimeAndOpaqueNextCursor()
    {
        var page = TrucoAdminAuditService.Parse("{\"success\":true,\"data\":{\"items\":[{\"id\":\"event1\",\"at\":\"2026-10-31T12:00:00Z\",\"actorId\":\"admin1\",\"action\":\"WALLET_APPROVE\",\"targetType\":\"wallet_request\",\"targetId\":\"request1\",\"reason\":\"Comprobante verificado\"}],\"nextCursor\":\"opaque-cursor\"}}");
        Assert.AreEqual(1, page.items.Count); Assert.AreEqual("event1", page.items[0].id);
        Assert.AreEqual("2026-10-31T12:00:00Z", page.items[0].at);
        Assert.AreEqual("admin1", page.items[0].actorId); Assert.AreEqual("opaque-cursor", page.nextCursor);
    }
    [Test]
    public void EmptyServerAuditDoesNotFabricateLocalEvents()
    {
        var page = TrucoAdminAuditService.Parse("{\"success\":true,\"data\":{\"items\":[],\"nextCursor\":null}}");
        Assert.IsEmpty(page.items); Assert.IsNull(page.nextCursor);
    }
    [TestCase("{\"success\":true,\"data\":{}}")]
    [TestCase("{\"success\":true,\"data\":{\"items\":[{}]}}")]
    [TestCase("{\"success\":false,\"data\":{\"items\":[]}}")]
    [TestCase("{\"success\":true,\"data\":{\"ok\":false,\"items\":[]}}")]
    public void MalformedOrRejectedAuditIsAnErrorRatherThanAnEmptySuccessfulFeed(string json) =>
        Assert.Throws<InvalidOperationException>(() => TrucoAdminAuditService.Parse(json));
}
