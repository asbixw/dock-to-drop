using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class ProjectBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Dock to Drop/Prepare Scene")]
    public static void PrepareScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        ConfigureTextures();
        ConfigureModels();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Bootstrap");
        go.AddComponent<GameFlow>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "Dock to Drop";
        PlayerSettings.productName = "Dock to Drop";
        PlayerSettings.bundleVersion = "0.3.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 170f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.antiAliasing = 4;
        AssetDatabase.SaveAssets();
        Debug.Log("Dock to Drop scene prepared.");
    }

    static void ConfigureModels()
    {
        var mi = AssetImporter.GetAtPath("Assets/Resources/Models/Driver.fbx") as ModelImporter;
        if (mi != null && mi.animationType != ModelImporterAnimationType.Legacy)
        {
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.animationWrapMode = WrapMode.Loop;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources/Models/Props" }))
        {
            var pi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (pi == null || pi.animationType == ModelImporterAnimationType.None) continue;
            pi.animationType = ModelImporterAnimationType.None;
            pi.importAnimation = false;
            pi.SaveAndReimport();
        }
    }

    static void ConfigureTextures()
    {
        foreach (var dir in new[] { "Assets/Resources/Textures/Props", "Assets/Resources/Textures" })
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                var t = AssetImporter.GetAtPath(path) as TextureImporter;
                if (t == null) continue;
                bool changed = false;
                if (file.EndsWith("_nor") && t.textureType != TextureImporterType.NormalMap) { t.textureType = TextureImporterType.NormalMap; changed = true; }
                if (file.EndsWith("_ms") && t.sRGBTexture) { t.sRGBTexture = false; changed = true; }
                if (changed) t.SaveAndReimport();
            }
        }
        string[] normals = { "warehouse_siding_normal", "asphalt_02_nor_gl", "concrete_floor_02_nor_gl", "asphalt_hq_n", "concrete_hq_n" };
        foreach (var n in normals)
        {
            var ti = AssetImporter.GetAtPath("Assets/Resources/Textures/" + n + ".jpg") as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
        string[] signs = { "amazon_logo_white", "amazon_logo_navy", "amazon_logo_black", "amazon_gate", "travel_center", "highway_sign", "speed_15", "dock_a14", "oil_stain", "tire_marks", "crack_a", "asphalt_patch", "ground_grime", "wet_patch" };
        foreach (var n in signs)
        {
            var ti = AssetImporter.GetAtPath("Assets/Resources/Textures/" + n + ".png") as TextureImporter;
            if (ti == null) continue;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        foreach (var n in new[] { "warehouse_siding", "yard_concrete", "road_asphalt", "ground_dirt", "asphalt_hq", "concrete_hq", "dirt_hq", "grass_hq" })
        {
            var ti = AssetImporter.GetAtPath("Assets/Resources/Textures/" + n + ".jpg") as TextureImporter;
            if (ti == null) continue;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }
    }

    public static void BuildMac()
    {
        PrepareScene();
        Directory.CreateDirectory("Build/Mac");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/Mac/DockToDrop.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("MAC BUILD: " + report.summary.result + " " + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildWebGL()
    {
        PrepareScene();
        Directory.CreateDirectory("Build/WebGL");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("WEBGL BUILD: " + report.summary.result + " " + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
