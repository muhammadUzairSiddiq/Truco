using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Searchable API catalog and explicit public runtime-configuration export.</summary>
[CustomEditor(typeof(TrucoApiEndpointsSO))]
public sealed class TrucoApiEndpointsSOEditor : Editor
{
    string _search = "";
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Configuración de API", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Los cambios de rutas compatibles pueden llegar por el bootstrap sin una nueva APK. Cambiar métodos, contratos JSON, lógica o dominios no autorizados requiere un cliente compatible.", MessageType.Info);
        Draw("baseUrl", "Base URL (HTTPS)");
        Draw("fetchRemoteConfiguration", "Cargar configuración remota");
        Draw("remoteConfigurationUrl", "Bootstrap URL público (HTTPS)");
        Draw("remoteConfigurationRevision", "Revisión de configuración");
        Draw("configurationTimeoutSeconds", "Timeout de configuración (segundos)");
        Draw("additionalTrustedApiOrigins", "Orígenes HTTPS adicionales de confianza");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Catálogo de endpoints", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _search = EditorGUILayout.TextField("Buscar clave / ruta", _search);
        if (GUILayout.Button("Limpiar", GUILayout.Width(60))) { _search = ""; GUI.FocusControl(null); }
        EditorGUILayout.EndHorizontal();
        var endpoints = serializedObject.FindProperty("endpoints");
        if (endpoints != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(endpoints.arraySize + " endpoints en el catálogo");
            if (GUILayout.Button("Expandir", GUILayout.Width(74))) Expand(endpoints, true);
            if (GUILayout.Button("Contraer", GUILayout.Width(74))) Expand(endpoints, false);
            EditorGUILayout.EndHorizontal();
            int shown = 0;
            for (int i = 0; i < endpoints.arraySize; i++)
            {
                var row = endpoints.GetArrayElementAtIndex(i);
                var key = row.FindPropertyRelative("key");
                var path = row.FindPropertyRelative("pathTemplate");
                var method = row.FindPropertyRelative("method");
                string keyText = key?.stringValue ?? "", pathText = path?.stringValue ?? "";
                if (!string.IsNullOrWhiteSpace(_search) && keyText.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0
                    && pathText.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                row.isExpanded = EditorGUILayout.Foldout(row.isExpanded,
                    (string.IsNullOrEmpty(keyText) ? "Endpoint sin clave" : keyText) + "  [" + (method?.stringValue ?? "") + "]", true);
                bool remove = GUILayout.Button("Eliminar", GUILayout.Width(65));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(pathText, EditorStyles.wordWrappedMiniLabel);
                if (row.isExpanded)
                {
                    if (key != null) EditorGUILayout.PropertyField(key, new GUIContent("Clave del cliente"));
                    if (method != null) EditorGUILayout.PropertyField(method, new GUIContent("Método HTTP"));
                    if (path != null) EditorGUILayout.PropertyField(path, new GUIContent("Ruta · {0} para ID"));
                }
                EditorGUILayout.EndVertical();
                if (remove) { endpoints.DeleteArrayElementAtIndex(i); break; }
            }
            if (shown == 0) EditorGUILayout.HelpBox("No hay endpoints que coincidan con la búsqueda.", MessageType.None);
            if (GUILayout.Button("Añadir endpoint"))
            {
                int index = endpoints.arraySize++; var row = endpoints.GetArrayElementAtIndex(index);
                var key = row.FindPropertyRelative("key"); var path = row.FindPropertyRelative("pathTemplate"); var method = row.FindPropertyRelative("method");
                if (key != null) key.stringValue = "";
                if (path != null) path.stringValue = "/";
                if (method != null) method.stringValue = "GET";
                row.isExpanded = true; _search = "";
            }
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        DrawExport((TrucoApiEndpointsSO)target);
    }

    void Draw(string name, string label)
    {
        var property = serializedObject.FindProperty(name);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }
    static void Expand(SerializedProperty array, bool expanded)
    { for (int i = 0; i < array.arraySize; i++) array.GetArrayElementAtIndex(i).isExpanded = expanded; }

    void DrawExport(TrucoApiEndpointsSO settings)
    {
        EditorGUILayout.LabelField("Exportar configuración pública", EditorStyles.boldLabel);
        var revisionProperty = serializedObject.FindProperty("remoteConfigurationRevision");
        int revision = revisionProperty != null ? revisionProperty.intValue : 1;
        var payload = new TrucoRemoteApiConfiguration.Payload {
            schemaVersion = 1, revision = revision,
            minimumClientContractVersion = TrucoRemoteApiConfiguration.ClientContractVersion,
            apiBaseUrl = settings.baseUrl?.TrimEnd('/'),
            endpoints = settings.endpoints?.Select(endpoint => endpoint == null ? null : new TrucoApiEndpointsSO.Endpoint {
                key = endpoint.key, method = endpoint.method, pathTemplate = endpoint.pathTemplate
            }).ToArray()
        };
        bool valid = TrucoRemoteApiConfiguration.Validate(payload, settings, 0, out string error);
        if (!valid) EditorGUILayout.HelpBox("No se puede exportar: " + error, MessageType.Error);
        EditorGUILayout.HelpBox("El JSON contiene solamente versión, revisión, base URL y rutas públicas. No incluye sesiones, cookies, tokens ni secretos. Incrementá la revisión antes de publicar una actualización compatible en el bootstrap.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!valid))
        {
            if (!GUILayout.Button("Exportar JSON de configuración runtime…")) return;
            string path = EditorUtility.SaveFilePanel("Exportar configuración pública de Truco", "",
                "truco-api-config-v1-r" + revision + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            // Explicit allowlist: never serialize the whole SO or any authentication/settings objects.
            var envelope = new TrucoRemoteApiConfiguration.Envelope { success = true, data = payload };
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(envelope, true), new UTF8Encoding(false));
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            { EditorUtility.DisplayDialog("No se pudo exportar", exception.Message, "Cerrar"); }
        }
    }
}
