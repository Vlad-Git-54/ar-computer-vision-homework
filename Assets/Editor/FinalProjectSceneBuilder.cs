// Автор: Марьяновский Владислав Андреевич

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FinalProjectSceneBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/FinalMainMenuScene.unity";
    private const string GameScenePath = "Assets/Scenes/FinalGameScene.unity";

    [MenuItem("Tools/Final Project/Rebuild Scenes")]
    public static void BuildFinalProject()
    {
        CreateMainMenuScene();
        CreateGameScene();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Финальные сцены проекта созданы");
    }

    private static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "FinalMainMenuScene";

        var controllerObject = new GameObject("Final Main Menu Controller");
        controllerObject.AddComponent<FinalMainMenuController>();
        controllerObject.AddComponent<FinalWebcamGestureInput>();

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.AddComponent<AudioListener>();

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    private static void CreateGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "FinalGameScene";

        var controllerObject = new GameObject("Final Game Controller");
        var controller = controllerObject.AddComponent<FinalGameController>();
        controllerObject.AddComponent<FinalWebcamGestureInput>();
        controller.robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/KyleRobot.prefab");
        controller.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        controller.playerAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerRobotAnimator.controller");
        controller.enemyAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/EnemyRobotAnimator.controller");
        controller.backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Open World Happiness Full.wav");
        controller.coinPickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/CoinPickup.wav");

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 4.3f, -7.4f);
        cameraObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        cameraObject.AddComponent<AudioListener>();

        var lightObject = new GameObject("Preview Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(54f, -32f, 0f);

        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }
}
