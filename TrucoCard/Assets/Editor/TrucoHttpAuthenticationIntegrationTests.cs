using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class TrucoHttpAuthenticationIntegrationTests
{
    [UnityTest]
    public IEnumerator UnauthorizedTournamentRequestRefreshesJsonTokensAndRetriesWithTheNewSession()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string previousBase = ApiConfig.BaseUrl;
        var requests = new List<string>();
        ApiConfig.BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        HttpApiClient.ClearAuthSession();
        HttpApiClient.AcceptAuthenticationResponse("{\"token\":\"expired\",\"refreshToken\":\"renew-me\"}");
        var server = Task.Run(async () =>
        {
            foreach (var response in new[] {
                (401, "{\"error\":\"Unauthorized\"}"),
                (200, "{\"success\":true,\"data\":{\"accessToken\":\"renewed\",\"refreshToken\":\"rotated\"}}"),
                (200, "{\"success\":true,\"tournament\":\"cup\"}") })
            {
                using (var client = await listener.AcceptTcpClientAsync())
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                {
                    var request = new StringBuilder();
                    int length = 0; string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        request.AppendLine(line);
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            length = int.Parse(line.Substring(15).Trim());
                    }
                    var body = new char[length]; int received = 0;
                    while (received < length) received += await reader.ReadAsync(body, received, length - received);
                    request.Append(body); requests.Add(request.ToString());
                    byte[] bytes = Encoding.UTF8.GetBytes(response.Item2);
                    byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {response.Item1} OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, 0, headers.Length);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        });
        try
        {
            var result = HttpApiClient.GetAsync(ApiConfig.BaseUrl + "/tournaments/cup");
            var deadline = DateTime.UtcNow.AddSeconds(40);
            while ((!result.IsCompleted || !server.IsCompleted) && DateTime.UtcNow < deadline) yield return null;
            Assert.IsTrue(result.IsCompleted && server.IsCompleted, "Refresh flow must finish within the request timeout.");
            Assert.IsNull(result.Exception); Assert.IsNull(server.Exception);
            StringAssert.Contains("cup", result.Result);
            Assert.AreEqual(3, requests.Count);
            StringAssert.Contains("Authorization: Bearer expired", requests[0]);
            StringAssert.Contains("POST /auth/refresh", requests[1]);
            StringAssert.Contains("refresh_token=renew-me", requests[1]);
            StringAssert.Contains("\"refreshToken\":\"renew-me\"", requests[1]);
            StringAssert.Contains("Authorization: Bearer renewed", requests[2]);
            StringAssert.Contains("access_token=renewed", requests[2]);
            StringAssert.Contains("refresh_token=rotated", requests[2]);
            StringAssert.DoesNotContain("Bearer expired", requests[2]);
        }
        finally { listener.Stop(); ApiConfig.BaseUrl = previousBase; HttpApiClient.ClearAuthSession(); }
    }
}
