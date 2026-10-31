using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TrucoReporterInputTests
{
    [TestCase("isGestureDone")]
    [TestCase("isDoubleClickDone")]
    [TestCase("getDownPos")]
    [TestCase("getDrag")]
    public void DiagnosticPointerReaderUsesTheConfiguredInputBackend(string method)
    {
        var root = new GameObject("ReporterInputRegression"); root.SetActive(false);
        try
        {
            var reporter = root.AddComponent<Reporter>();
            var read = typeof(Reporter).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(read);
            Assert.DoesNotThrow(() => read.Invoke(reporter, null),
                "Input System-only settings must not throw UnityEngine.Input exceptions every frame.");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
