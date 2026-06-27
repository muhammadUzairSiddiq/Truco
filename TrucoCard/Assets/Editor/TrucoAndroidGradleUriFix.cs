#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Gradle/AGP fails on Windows when the Firebase local maven repo uses a file:// URI
/// with unencoded spaces (e.g. project path "UNITY PROJECTS"). Re-apply after EDM Force Resolve.
/// </summary>
public class TrucoAndroidGradleUriFix : IPreprocessBuildWithReport
{
    const string TemplateAsset = "Assets/Plugins/Android/settingsTemplate.gradle";
    const string BrokenMarker = "def unityProjectPath = $/file:///";

    const string FixedBlock = @"// Android Resolver Repos Start
        def firebaseMavenRepo = new File(""**DIR_UNITYPROJECT**"", ""Assets/GeneratedLocalRepo/Firebase/m2repository"")
        maven {
            url firebaseMavenRepo.toURI() // Assets/Firebase/Editor/AppDependencies.xml:22, Assets/Firebase/Editor/AuthDependencies.xml:20, Assets/Firebase/Editor/DatabaseDependencies.xml:22
        }
        mavenLocal()
// Android Resolver Repos End";

    public int callbackOrder => -100;

    [InitializeOnLoadMethod]
    static void ScheduleTemplateCheck() => EditorApplication.delayCall += () => FixTemplateIfNeeded(false);

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        FixTemplateIfNeeded(true);
    }

    static void FixTemplateIfNeeded(bool log)
    {
        var path = Path.Combine(Application.dataPath, "Plugins/Android/settingsTemplate.gradle");
        if (!File.Exists(path)) return;
        var text = File.ReadAllText(path);
        if (!text.Contains(BrokenMarker)) return;

        var patched = Regex.Replace(
            text,
            @"// Android Resolver Repos Start.*?// Android Resolver Repos End",
            FixedBlock,
            RegexOptions.Singleline);

        if (patched == text) return;
        File.WriteAllText(path, patched);
        AssetDatabase.ImportAsset(TemplateAsset);
        if (log)
            Debug.Log("[TrucoAndroidGradleUriFix] Patched settingsTemplate.gradle for Windows paths with spaces.");
    }
}
#endif
