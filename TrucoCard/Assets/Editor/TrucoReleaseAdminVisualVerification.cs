using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Four actual admin UI captures with explicit contact-free Editor fixtures, followed by release preflight.</summary>
public static class TrucoReleaseAdminVisualVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run admin visual verification in an isolated batch QA project.");
        Directory.CreateDirectory(Path.GetFullPath("Artifacts/UI/PlayerGallery"));
        var buy = Fixture("EDITOR-COMPRA-001", "buy", 100, "requested", "2026-10-31T12:10:00Z");
        var sell = Fixture("EDITOR-VENTA-002", "sell", 250, "approved", "2026-10-31T12:00:00Z");
        buy.allowedActions = new[] { "approve", "reject" };
        buy.receiptDownloadUrl = "https://example.invalid/editor/receipt.pdf";
        sell.allowedActions = new[] { "pay" };
        sell.revision = 2;
        sell.request.history.Add(new TrucoWalletService.Stage { status = "approved", at = "2026-10-31T12:05:00Z" });
        sell.audit.Add(new TrucoWalletAdminService.AuditEntry {
            id = "editor-sale-approved", action = "approve", actorId = "editor-admin-fixture",
            at = "2026-10-31T12:05:00Z", reason = "Ejemplo aprobado; transferencia pendiente.",
            idempotencyKey = "editor-sale-approval-fixture", revision = 2
        });
        try
        {
            Capture("release-admin-01", new[] { buy, sell });
            Capture("release-admin-02", null, buy);
            Capture("release-admin-03", null, sell);
            Capture("release-admin-04", Array.Empty<TrucoWalletAdminService.Record>());
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        Debug.Log("[RELEASE ADMIN VISUAL QA] Four explicit Editor-fixture captures saved. No HTTP or financial operations performed.");
        TrucoReleasePreflight.Run();
    }

    static void Capture(string name, TrucoWalletAdminService.Record[] records, TrucoWalletAdminService.Record detail = null)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = TrucoWalletAdminScreen.EditorPreview(records, detail);
        try { TrucoPlayerScreenGallery.Render(root, name); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static TrucoWalletAdminService.Record Fixture(string id, string type, int amount, string status, string createdAt)
    {
        return new TrucoWalletAdminService.Record {
            request = new TrucoWalletService.Request {
                id = id, type = type, amount = amount, status = status, createdAt = createdAt,
                bank = "Banco de ejemplo", alias = "Cuenta de prueba", holder = "Titular de prueba",
                history = new List<TrucoWalletService.Stage> { new TrucoWalletService.Stage { status = "requested", at = createdAt } }
            },
            owner = new TrucoWalletAdminService.Owner { id = "editor-player-fixture", username = "Jugador de ejemplo" },
            revision = 1,
            audit = new List<TrucoWalletAdminService.AuditEntry> { new TrucoWalletAdminService.AuditEntry {
                id = "editor-request-" + type, action = "requested", actorId = "editor-player-fixture",
                at = createdAt, reason = "Solicitud de ejemplo para revisión visual.",
                idempotencyKey = "editor-request-fixture-" + type, revision = 1
            } }
        };
    }
}
