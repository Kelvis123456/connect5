#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Menu: Connect5 → Setup Scenes  (Ctrl+Alt+S)
/// Builds LobbyScene and GameScene with UI Toolkit.
/// After saving the scene YAML Unity serialises m_PanelSettings as {fileID:0}
/// regardless of any API call — a known Unity 6 editor scripting limitation.
/// We fix it by patching the YAML text directly after each SaveScene call.
/// </summary>
public static class Connect5Setup
{
    const string ScenesFolder = "Assets/Scenes";
    const string UIFolder     = "Assets/UI";
    const string PanelPath    = "Assets/UI/Connect5PanelSettings.asset";
    const string LobbyScene   = "LobbyScene";
    const string GameScene    = "GameScene";
    const string LobbyUXML    = "Assets/UI/Lobby.uxml";
    const string GameUXML     = "Assets/UI/Game.uxml";

    // ── Entry point ──────────────────────────────────────────────────────────

    [MenuItem("Connect5/Setup Scenes %&s", priority = 1)]
    public static void SetupAll()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "UI", "Lobby.uxml")))
        {
            Debug.LogError("[Connect5Setup] Assets/UI/Lobby.uxml not found.");
            return;
        }

        AssetDatabase.Refresh();
        EnsureFolder(ScenesFolder);
        EnsureFolder(UIFolder);

        GetOrCreatePanelSettings();   // ensures the asset exists on disk
        string guid = AssetDatabase.AssetPathToGUID(PanelPath);

        BuildLobbyScene(guid);
        BuildGameScene(guid);
        RegisterBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();   // re-imports the patched YAML files
        Debug.Log("✅ Connect5: escenas creadas. Abre LobbyScene y pulsa Play.");
    }

    // ══════════════════════════════════════════════════════════════════════════

    static void BuildLobbyScene(string psGuid)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddCamera();
        AddEventSystem();
        AddNetworkManager();
        new GameObject("RelayManager").AddComponent<RelayManager>();

        var uiGO  = new GameObject("UI");
        var uiDoc = uiGO.AddComponent<UIDocument>();
        SetUXML(uiDoc, LobbyUXML);
        uiGO.AddComponent<LobbyUI>();

        string scenePath = $"{ScenesFolder}/{LobbyScene}.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        PatchPanelSettings(scenePath, psGuid);   // YAML patch — only reliable method
    }

    static void BuildGameScene(string psGuid)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddCamera();
        AddEventSystem();
        new GameObject("GameManager").AddComponent<GameManager>();

        var ngmGO = new GameObject("NetworkGameManager");
        ngmGO.AddComponent<NetworkObject>();
        ngmGO.AddComponent<NetworkGameManager>();

        var uiGO  = new GameObject("UI");
        var uiDoc = uiGO.AddComponent<UIDocument>();
        SetUXML(uiDoc, GameUXML);
        uiGO.AddComponent<GameUI>();

        string scenePath = $"{ScenesFolder}/{GameScene}.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        PatchPanelSettings(scenePath, psGuid);   // YAML patch — only reliable method
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Sets only the UXML — this DOES serialize reliably via SerializedObject.
    static void SetUXML(UIDocument uiDoc, string uxmlPath)
    {
        var vta  = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        var so   = new SerializedObject(uiDoc);
        var prop = so.FindProperty("sourceAsset");
        if (prop != null) { prop.objectReferenceValue = vta; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    // m_PanelSettings refuses to persist via any Unity editor API in Unity 6.
    // Reading and rewriting the YAML file is the only approach that works.
    static void PatchPanelSettings(string assetRelPath, string psGuid)
    {
        string fullPath = Application.dataPath + assetRelPath.Substring("Assets".Length);
        if (!File.Exists(fullPath)) { Debug.LogError($"[Connect5Setup] Scene not found: {fullPath}"); return; }

        string yaml = File.ReadAllText(fullPath, Encoding.UTF8);
        string patched = yaml.Replace(
            "m_PanelSettings: {fileID: 0}",
            $"m_PanelSettings: {{fileID: 11400000, guid: {psGuid}, type: 2}}");

        if (yaml == patched)
            Debug.LogWarning($"[Connect5Setup] PanelSettings patch: no replacement found in {assetRelPath}");
        else
            File.WriteAllText(fullPath, patched, Encoding.UTF8);
    }

    static PanelSettings GetOrCreatePanelSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (existing != null) return existing;

        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode             = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution   = new Vector2Int(1920, 1080);
        ps.screenMatchMode       = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match                 = 0.5f;
        AssetDatabase.CreateAsset(ps, PanelPath);
        AssetDatabase.SaveAssets();
        return ps;
    }

    static void AddCamera()
    {
        var go  = new GameObject("Main Camera");
        go.tag  = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.039f, 0.063f, 0.125f);
        cam.orthographic    = true;
        cam.depth           = -1;
        go.AddComponent<AudioListener>();
    }

    static void AddEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var t = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        go.AddComponent(t ?? typeof(StandaloneInputModule));
    }

    static void AddNetworkManager()
    {
        var go        = new GameObject("[NetworkManager]");
        var nm        = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        try   { nm.NetworkConfig.NetworkTransport = transport; }
        catch { Debug.LogWarning("[Connect5Setup] Asigna UnityTransport al NetworkManager manualmente."); }
    }

    static void RegisterBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{ScenesFolder}/{LobbyScene}.unity", true),
            new EditorBuildSettingsScene($"{ScenesFolder}/{GameScene}.unity",  true),
        };
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
