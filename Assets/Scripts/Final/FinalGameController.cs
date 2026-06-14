// Автор: Марьяновский Владислав Андреевич

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FinalGameController : MonoBehaviour
{
    [Header("Scene references")]
    public GameObject robotPrefab;
    public GameObject coinPrefab;
    public RuntimeAnimatorController playerAnimatorController;
    public RuntimeAnimatorController enemyAnimatorController;
    public AudioClip backgroundMusic;
    public AudioClip coinPickupSound;

    [Header("Gameplay")]
    [SerializeField] private string menuSceneName = "FinalMainMenuScene";
    [SerializeField] private int coinCount = 32;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float playerMoveSpeed = 4.4f;
    [SerializeField] private float enemyMoveSpeed = 3.95f;
    [SerializeField] private Vector2 arenaSize = new Vector2(42f, 24f);
    [SerializeField] private float gestureHoldTime = 0.8f;

    private const string PlayerColorKey = "FinalProjectPlayerColor";
    private const string HighScoreKey = "FinalProjectHighScore";

    private readonly List<NavMeshBoxSource> navMeshBoxes = new List<NavMeshBoxSource>();
    private readonly List<GameObject> coins = new List<GameObject>();
    private readonly List<GameObject> enemies = new List<GameObject>();
    private readonly Color floorColor = new Color(0.56f, 0.82f, 0.75f, 1f);
    private readonly Color wallColor = new Color(0.55f, 0.63f, 0.76f, 1f);
    private readonly Vector3 cameraOffset = new Vector3(0f, 10.5f, -13.5f);
    private const float CameraLookHeight = 1.25f;

    private NavMeshData navMeshData;
    private NavMeshDataInstance navMeshInstance;
    private Transform arenaRoot;
    private Transform playerTransform;
    private FinalPlayerController playerController;
    private Camera gameCamera;
    private FinalWebcamGestureInput gestureInput;
    private AudioSource musicSource;
    private Canvas hudCanvas;
    private Text scoreText;
    private Text bestScoreText;
    private Text centerMessageText;
    private GameObject pausePanel;
    private GameObject finalPanel;
    private Text finalTitleText;
    private Text finalScoreText;
    private Text finalBestText;
    private Image pauseLeftProgress;
    private Image pauseRightProgress;
    private Image finalLeftProgress;
    private Image finalRightProgress;
    private Sprite whiteSprite;
    private int score;
    private int highScore;
    private bool paused;
    private bool gameEnded;

    private struct NavMeshBoxSource
    {
        public Vector3 Position;
        public Vector3 Size;
        public int Area;
        public NavMeshBuildSourceShape Shape;

        public NavMeshBoxSource(Vector3 position, Vector3 size, int area, NavMeshBuildSourceShape shape)
        {
            Position = position;
            Size = size;
            Area = area;
            Shape = shape;
        }
    }

    public bool IsGameEnded => gameEnded;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);

        CreateEventSystemIfNeeded();
        CreateLighting();
        CreateArena();
        BuildRuntimeNavMesh();
        CreateCamera();
        SpawnPlayer();
        SpawnEnemies();
        SpawnCoins();
        CreatePointClouds();
        CreateAudio();
        CreateHud();
        UpdateScoreView();
        ShowCenterMessage("Соберите все монеты и не дайте врагам догнать себя", 2.8f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !gameEnded)
        {
            SetPaused(!paused);
        }

        if (paused)
        {
            UpdatePauseGestureMenu();
        }

        if (gameEnded)
        {
            UpdateFinalGestureMenu();
        }
    }

    private void OnDestroy()
    {
        if (navMeshInstance.valid)
        {
            navMeshInstance.Remove();
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public void CollectCoin(Vector3 position)
    {
        if (gameEnded)
        {
            return;
        }

        score++;
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }

        PlayCoinSound(position);
        CreateCoinBurst(position);
        UpdateScoreView();

        if (score >= coinCount)
        {
            WinGame();
        }
    }

    public void LoseGame(string reason)
    {
        if (gameEnded)
        {
            return;
        }

        FinishGame("Поражение", reason);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(menuSceneName);
    }

    private void WinGame()
    {
        FinishGame("Победа", "Все монеты собраны");
    }

    private void FinishGame(string title, string reason)
    {
        gameEnded = true;
        paused = false;
        if (playerController != null)
        {
            playerController.SetControlEnabled(false);
        }

        Time.timeScale = 0f;
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (finalTitleText != null)
        {
            finalTitleText.text = title;
        }

        if (finalScoreText != null)
        {
            finalScoreText.text = reason + "\nСчет: " + score;
        }

        if (finalBestText != null)
        {
            finalBestText.text = "Рекорд: " + highScore;
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (finalPanel != null)
        {
            finalPanel.SetActive(true);
        }
    }

    private void CreateLighting()
    {
        var lightObject = new GameObject("Final Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f, 1f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(54f, -32f, 0f);

        var fillObject = new GameObject("Final Fill Light");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.68f, 0.78f, 1f, 1f);
        fill.intensity = 0.45f;
        fill.shadows = LightShadows.None;
        fillObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);

        RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.7f, 1f);
        RenderSettings.fog = false;
    }

    private void CreateArena()
    {
        arenaRoot = new GameObject("Final Gameplay Arena").transform;
        CreateCube("Large Gameplay Floor", new Vector3(0f, -0.12f, 0f), new Vector3(arenaSize.x, 0.24f, arenaSize.y), floorColor, true);

        CreateCube("North Wall", new Vector3(-7f, 1.2f, 9.6f), new Vector3(20f, 2.4f, 0.48f), wallColor, true);
        CreateCube("West Inner Wall", new Vector3(-10.5f, 1.1f, -2.5f), new Vector3(0.5f, 2.2f, 11f), wallColor, true);
        CreateCube("Center Wall", new Vector3(0f, 1.1f, 0f), new Vector3(11f, 2.2f, 0.5f), wallColor, true);
        CreateCube("East Inner Wall", new Vector3(10.5f, 1.1f, 2.5f), new Vector3(0.5f, 2.2f, 10f), wallColor, true);
        CreateCube("Short Cover Left", new Vector3(-3.5f, 0.8f, -7f), new Vector3(5.4f, 1.6f, 0.55f), wallColor, true);
        CreateCube("Short Cover Right", new Vector3(6f, 0.8f, -5.5f), new Vector3(5.4f, 1.6f, 0.55f), wallColor, true);

        CreateCube("Box Obstacle A", new Vector3(-15f, 0.7f, 6f), new Vector3(2.2f, 1.4f, 2.2f), new Color(0.46f, 0.5f, 0.58f, 1f), true);
        CreateCube("Box Obstacle B", new Vector3(15.5f, 0.7f, -6f), new Vector3(2.4f, 1.4f, 2.4f), new Color(0.46f, 0.5f, 0.58f, 1f), true);
        CreateCube("Box Obstacle C", new Vector3(15f, 0.5f, 7f), new Vector3(1.6f, 1f, 3.6f), new Color(0.46f, 0.5f, 0.58f, 1f), true);
        CreateCube("Box Obstacle D", new Vector3(-17f, 0.5f, -7.5f), new Vector3(3.2f, 1f, 1.6f), new Color(0.46f, 0.5f, 0.58f, 1f), true);
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Color color, bool includeInNavMesh)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(arenaRoot, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.isStatic = true;

        var renderer = cube.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = color;

        if (includeInNavMesh)
        {
            RegisterNavMeshBox(objectName, position, scale);
        }

        return cube;
    }

    private void RegisterNavMeshBox(string objectName, Vector3 position, Vector3 size)
    {
        var notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0)
        {
            notWalkableArea = 1;
        }

        var isFloor = objectName.Contains("Floor");
        var area = isFloor ? 0 : notWalkableArea;
        var shape = isFloor ? NavMeshBuildSourceShape.Box : NavMeshBuildSourceShape.ModifierBox;
        navMeshBoxes.Add(new NavMeshBoxSource(position, size, area, shape));
    }

    private void BuildRuntimeNavMesh()
    {
        var sources = new List<NavMeshBuildSource>();
        foreach (var box in navMeshBoxes)
        {
            sources.Add(new NavMeshBuildSource
            {
                shape = box.Shape,
                transform = Matrix4x4.TRS(box.Position, Quaternion.identity, Vector3.one),
                size = box.Size,
                area = box.Area
            });
        }

        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.35f;
        settings.agentHeight = 1.8f;
        settings.agentClimb = 0.35f;
        settings.minRegionArea = 0.08f;

        var bounds = new Bounds(Vector3.zero, new Vector3(arenaSize.x + 6f, 8f, arenaSize.y + 6f));
        navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (navMeshData != null)
        {
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
        }
        else
        {
            Debug.LogError("Не удалось построить NavMesh для финальной арены");
        }
    }

    private void CreateCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            gameCamera = FindObjectOfType<Camera>();
        }

        GameObject cameraObject;
        if (gameCamera == null)
        {
            cameraObject = new GameObject("Main Camera");
            gameCamera = cameraObject.AddComponent<Camera>();
        }
        else
        {
            cameraObject = gameCamera.gameObject;
        }

        cameraObject.tag = "MainCamera";
        gameCamera.fieldOfView = 54f;
        gameCamera.nearClipPlane = 0.05f;
        gameCamera.farClipPlane = 200f;
        gameCamera.clearFlags = CameraClearFlags.Skybox;

        if (cameraObject.GetComponent<AudioListener>() == null)
        {
            cameraObject.AddComponent<AudioListener>();
        }

        var follow = cameraObject.GetComponent<ThirdPersonCameraFollow>();
        if (follow == null)
        {
            follow = cameraObject.AddComponent<ThirdPersonCameraFollow>();
        }

        SetPrivateField(follow, "offset", cameraOffset);
        SetPrivateField(follow, "followSpeed", 9f);
        SetPrivateField(follow, "lookHeight", CameraLookHeight);
    }

    private void SpawnPlayer()
    {
        var playerColorIndex = PlayerPrefs.GetInt(PlayerColorKey, 0);
        var color = playerColorIndex == 0 ? new Color(0.18f, 0.66f, 1f, 1f) : new Color(1f, 0.18f, 0.14f, 1f);
        var player = CreateRobotActor("Robot Player", new Vector3(0f, 0.02f, -8.5f), color, playerAnimatorController);

        playerTransform = player.transform;
        var rigidbody = player.AddComponent<Rigidbody>();
        rigidbody.mass = 1.2f;
        rigidbody.drag = 0f;
        rigidbody.angularDrag = 0.05f;

        player.AddComponent<CapsuleCollider>();
        playerController = player.AddComponent<FinalPlayerController>();
        SetPrivateField(playerController, "moveSpeed", playerMoveSpeed);
        playerController.Configure(this, gameCamera.transform, player.GetComponentInChildren<Animator>());

        var follow = gameCamera.GetComponent<ThirdPersonCameraFollow>();
        SetPrivateField(follow, "target", playerTransform);
        PlaceCameraAtPlayer();
    }

    private void SpawnEnemies()
    {
        var spawnPoints = new[]
        {
            new Vector3(-17f, 0f, 8f),
            new Vector3(17f, 0f, 8f),
            new Vector3(17f, 0f, -8f)
        };

        for (var i = 0; i < Mathf.Min(enemyCount, spawnPoints.Length); i++)
        {
            Vector3 spawn;
            if (!TryGetNavMeshPoint(spawnPoints[i], 8f, out spawn))
            {
                Debug.LogWarning("Пропущен враг, потому что рядом с точкой появления нет NavMesh: " + spawnPoints[i]);
                continue;
            }

            var enemy = CreateRobotActor("Enemy Robot " + (i + 1), spawn, new Color(0.95f, 0.18f, 0.12f, 1f), enemyAnimatorController);
            var agent = enemy.AddComponent<NavMeshAgent>();
            agent.Warp(spawn);
            enemy.AddComponent<CapsuleCollider>();
            var controller = enemy.AddComponent<FinalEnemyController>();
            controller.Configure(this, playerTransform, enemy.GetComponentInChildren<Animator>(), enemyMoveSpeed);
            enemies.Add(enemy);
        }
    }

    private bool TryGetNavMeshPoint(Vector3 point, float distance, out Vector3 result)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, distance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = point;
        return false;
    }

    private void PlaceCameraAtPlayer()
    {
        if (gameCamera == null || playerTransform == null)
        {
            return;
        }

        gameCamera.transform.position = playerTransform.position + cameraOffset;
        gameCamera.transform.LookAt(playerTransform.position + Vector3.up * CameraLookHeight);
    }

    private GameObject CreateRobotActor(string objectName, Vector3 position, Color color, RuntimeAnimatorController controller)
    {
        var root = new GameObject(objectName);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;

        GameObject model;
        if (robotPrefab != null)
        {
            model = Instantiate(robotPrefab, root.transform);
            model.name = "Kyle Robot Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 1.08f;
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Fallback Robot Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            model.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
        }

        foreach (var collider in model.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        var animator = model.GetComponentInChildren<Animator>();
        if (animator != null && controller != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        ApplyColor(model, color);
        return root;
    }

    private void ApplyColor(GameObject target, Color color)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (var currentRenderer in renderers)
        {
            foreach (var material in currentRenderer.materials)
            {
                material.color = Color.Lerp(material.color, color, 0.72f);
            }
        }
    }

    private void SpawnCoins()
    {
        var positions = CreateCoinPositions();
        for (var i = 0; i < Mathf.Min(coinCount, positions.Count); i++)
        {
            var coin = coinPrefab != null ? Instantiate(coinPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coin.name = "Final Coin " + (i + 1);
            coin.transform.position = positions[i];
            coin.transform.localScale = Vector3.one * 0.52f;

            DisableOldCoinScripts(coin);

            var collider = coin.GetComponent<Collider>();
            if (collider == null)
            {
                collider = coin.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;
            var pickup = coin.AddComponent<FinalCoinPickup>();
            pickup.Configure(this);
            CreateCoinGlow(coin.transform);
            coins.Add(coin);
        }
    }

    private List<Vector3> CreateCoinPositions()
    {
        var positions = new List<Vector3>();
        var rows = 4;
        var columns = 8;
        var usableWidth = arenaSize.x - 7f;
        var usableDepth = arenaSize.y - 5f;

        for (var z = 0; z < rows; z++)
        {
            for (var x = 0; x < columns; x++)
            {
                var px = -usableWidth * 0.5f + usableWidth * x / (columns - 1);
                var pz = -usableDepth * 0.5f + usableDepth * z / (rows - 1);
                var offset = new Vector3(Mathf.Sin((x + z) * 1.7f) * 0.8f, 0f, Mathf.Cos((x - z) * 1.2f) * 0.6f);
                var point = new Vector3(px, 0.55f, pz) + offset;
                if (IsPointClear(point))
                {
                    positions.Add(point);
                }
            }
        }

        var attempt = 0;
        while (positions.Count < coinCount && attempt < coinCount * 24)
        {
            var angle = attempt * 137.5f * Mathf.Deg2Rad;
            var radius = 2.4f + attempt % 12 * 1.45f;
            var point = new Vector3(
                Mathf.Clamp(Mathf.Cos(angle) * radius, -usableWidth * 0.48f, usableWidth * 0.48f),
                0.55f,
                Mathf.Clamp(Mathf.Sin(angle) * radius, -usableDepth * 0.48f, usableDepth * 0.48f));

            if (IsPointClear(point) && IsFarFromOtherCoins(point, positions))
            {
                positions.Add(point);
            }

            attempt++;
        }

        return positions;
    }

    private bool IsPointClear(Vector3 point)
    {
        var hits = Physics.OverlapSphere(point, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.gameObject.name.Contains("Wall") || hit.gameObject.name.Contains("Box"))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsFarFromOtherCoins(Vector3 point, List<Vector3> positions)
    {
        foreach (var position in positions)
        {
            if (Vector3.Distance(point, position) < 1.25f)
            {
                return false;
            }
        }

        return true;
    }

    private void DisableOldCoinScripts(GameObject coin)
    {
        var behaviours = coin.GetComponents<MonoBehaviour>();
        foreach (var behaviour in behaviours)
        {
            if (behaviour != null && behaviour.GetType().Name == "CoinCollectible")
            {
                behaviour.enabled = false;
            }
        }
    }

    private void CreateCoinGlow(Transform coin)
    {
        var glow = new GameObject("Coin Sparkle");
        glow.transform.SetParent(coin, false);
        glow.transform.localPosition = Vector3.zero;

        var particles = glow.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.25f;
        main.startSize = 0.055f;
        main.maxParticles = 16;
        main.loop = true;
        main.startColor = new Color(1f, 0.92f, 0.32f, 0.75f);

        var emission = particles.emission;
        emission.rateOverTime = 8f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
    }

    private void CreatePointClouds()
    {
        CreateCloudParticleSystem("Point Cloud A", new Vector3(-8f, 5.2f, 1.5f), new Vector3(4.6f, 1.2f, 1.6f));
        CreateCloudParticleSystem("Point Cloud B", new Vector3(8f, 5.5f, -2.5f), new Vector3(5.2f, 1.4f, 1.8f));
    }

    private void CreateCloudParticleSystem(string objectName, Vector3 position, Vector3 scale)
    {
        var cloud = new GameObject(objectName);
        cloud.transform.position = position;
        cloud.transform.localScale = scale;

        var particles = cloud.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0.05f;
        main.startSize = 0.18f;
        main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new Color(0.86f, 0.98f, 1f, 0.88f);

        var emission = particles.emission;
        emission.rateOverTime = 24f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.9f;

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;
    }

    private void CreateAudio()
    {
        var audioObject = new GameObject("Final Scene Audio");
        musicSource = audioObject.AddComponent<AudioSource>();
        musicSource.clip = backgroundMusic != null ? backgroundMusic : Resources.Load<AudioClip>("Audio/Open World Happiness Full");
        musicSource.volume = 0.32f;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        if (musicSource.clip != null)
        {
            musicSource.Play();
        }
    }

    private void PlayCoinSound(Vector3 position)
    {
        var clip = coinPickupSound != null ? coinPickupSound : Resources.Load<AudioClip>("Audio/CoinPickup");
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, 0.74f);
        }
    }

    private void CreateCoinBurst(Vector3 position)
    {
        var burstObject = new GameObject("Coin Pickup Particles");
        burstObject.transform.position = position;
        var particles = burstObject.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.duration = 0.45f;
        main.startLifetime = 0.55f;
        main.startSpeed = 2.5f;
        main.startSize = 0.09f;
        main.startColor = new Color(1f, 0.86f, 0.18f, 1f);
        main.loop = false;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        Destroy(burstObject, 1.25f);
    }

    private void CreateHud()
    {
        hudCanvas = new GameObject("Final HUD Canvas").AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 120;

        var scaler = hudCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        hudCanvas.gameObject.AddComponent<GraphicRaycaster>();

        CreateScoreHud(hudCanvas.transform);
        CreatePausePanel(hudCanvas.transform);
        CreateFinalPanel(hudCanvas.transform);
        CreateWebcamGestureHud(hudCanvas.transform);

        centerMessageText = CreateText("Center Message", hudCanvas.transform, "", 24, TextAnchor.MiddleCenter, new Color(0.9f, 1f, 0.9f, 1f));
        centerMessageText.fontStyle = FontStyle.Bold;
        SetAnchored(centerMessageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(780f, 54f), new Vector2(0.5f, 0f));

        pausePanel.SetActive(false);
        finalPanel.SetActive(false);
    }

    private void CreateScoreHud(Transform parent)
    {
        var panel = CreateImage("Score HUD", parent, new Color(0.04f, 0.06f, 0.08f, 0.82f));
        SetAnchored(panel.rectTransform, new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(330f, 104f), new Vector2(1f, 1f));

        var title = CreateText("Score Title", panel.transform, "Очки", 22, TextAnchor.MiddleLeft, Color.white);
        title.fontStyle = FontStyle.Bold;
        Stretch(title.rectTransform, new Vector2(18f, 54f), new Vector2(18f, 10f));

        scoreText = CreateText("Score Value", panel.transform, "0", 34, TextAnchor.MiddleRight, Color.white);
        scoreText.fontStyle = FontStyle.Bold;
        Stretch(scoreText.rectTransform, new Vector2(18f, 42f), new Vector2(18f, 10f));

        bestScoreText = CreateText("Best Score", panel.transform, "Рекорд: 0", 20, TextAnchor.MiddleLeft, new Color(0.82f, 0.9f, 1f, 1f));
        Stretch(bestScoreText.rectTransform, new Vector2(18f, 10f), new Vector2(18f, 58f));
    }

    private void CreatePausePanel(Transform parent)
    {
        pausePanel = CreateOverlayPanel("Pause Panel", parent, 0.64f);
        var box = CreateImage("Pause Box", pausePanel.transform, new Color(0.08f, 0.1f, 0.14f, 0.96f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 390f), new Vector2(0.5f, 0.5f));

        var title = CreateText("Pause Title", box.transform, "Пауза", 40, TextAnchor.MiddleCenter, Color.white);
        title.fontStyle = FontStyle.Bold;
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(540f, 56f), new Vector2(0.5f, 1f));

        CreateMenuButton(box.transform, "Продолжить", "Левая рука", new Vector2(-150f, -164f), ResumeGame, out pauseLeftProgress);
        CreateMenuButton(box.transform, "Выйти в меню", "Правая рука", new Vector2(150f, -164f), ExitToMenu, out pauseRightProgress);
    }

    private void CreateFinalPanel(Transform parent)
    {
        finalPanel = CreateOverlayPanel("Final Panel", parent, 0.72f);
        var box = CreateImage("Final Box", finalPanel.transform, new Color(0.08f, 0.1f, 0.14f, 0.97f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 460f), new Vector2(0.5f, 0.5f));

        finalTitleText = CreateText("Final Title", box.transform, "Игра окончена", 42, TextAnchor.MiddleCenter, Color.white);
        finalTitleText.fontStyle = FontStyle.Bold;
        SetAnchored(finalTitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(560f, 60f), new Vector2(0.5f, 1f));

        finalScoreText = CreateText("Final Score", box.transform, "", 25, TextAnchor.MiddleCenter, Color.white);
        SetAnchored(finalScoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(560f, 84f), new Vector2(0.5f, 1f));

        finalBestText = CreateText("Final Best", box.transform, "", 24, TextAnchor.MiddleCenter, new Color(0.82f, 0.9f, 1f, 1f));
        SetAnchored(finalBestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(560f, 42f), new Vector2(0.5f, 1f));

        CreateMenuButton(box.transform, "Заново", "Левая рука", new Vector2(-150f, -330f), RestartGame, out finalLeftProgress);
        CreateMenuButton(box.transform, "В меню", "Правая рука", new Vector2(150f, -330f), ExitToMenu, out finalRightProgress);
    }

    private void CreateWebcamGestureHud(Transform parent)
    {
        var panel = CreateImage("AR Gesture Panel", parent, new Color(0.04f, 0.06f, 0.08f, 0.72f));
        SetAnchored(panel.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(420f, 118f), new Vector2(0f, 1f));

        var title = CreateText("AR Gesture Title", panel.transform, "AR управление меню", 22, TextAnchor.MiddleLeft, Color.white);
        title.fontStyle = FontStyle.Bold;
        Stretch(title.rectTransform, new Vector2(18f, 56f), new Vector2(18f, 12f));

        var status = CreateText("AR Gesture Status", panel.transform, "", 18, TextAnchor.MiddleLeft, new Color(0.82f, 0.9f, 1f, 1f));
        Stretch(status.rectTransform, new Vector2(18f, 18f), new Vector2(18f, 58f));

        var preview = CreateRawImage("AR Gesture Preview", panel.transform);
        SetAnchored(preview.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(104f, 72f), new Vector2(1f, 0.5f));

        gestureInput = gameObject.AddComponent<FinalWebcamGestureInput>();
        gestureInput.SetPreview(preview);
        gestureInput.SetStatus(status);
    }

    private GameObject CreateOverlayPanel(string objectName, Transform parent, float opacity)
    {
        var panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = panel.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = new Color(0f, 0f, 0f, opacity);
        return panel;
    }

    private void CreateMenuButton(Transform parent, string label, string hint, Vector2 position, UnityEngine.Events.UnityAction action, out Image progress)
    {
        var buttonImage = CreateImage(label + " Button", parent, new Color(0.16f, 0.46f, 0.88f, 1f));
        SetAnchored(buttonImage.rectTransform, new Vector2(0.5f, 1f), position, new Vector2(250f, 112f), new Vector2(0.5f, 1f));

        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(action);

        var labelText = CreateText(label + " Label", buttonImage.transform, label, 24, TextAnchor.MiddleCenter, Color.white);
        labelText.fontStyle = FontStyle.Bold;
        SetAnchored(labelText.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(220f, 36f), new Vector2(0.5f, 0.5f));

        var hintText = CreateText(label + " Hint", buttonImage.transform, hint, 16, TextAnchor.MiddleCenter, new Color(0.86f, 0.94f, 1f, 1f));
        SetAnchored(hintText.rectTransform, new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(220f, 26f), new Vector2(0.5f, 0.5f));

        var progressBack = CreateImage(label + " Progress Back", buttonImage.transform, new Color(0f, 0f, 0f, 0.28f));
        SetAnchored(progressBack.rectTransform, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(194f, 12f), new Vector2(0.5f, 0.5f));

        progress = CreateImage(label + " Progress", progressBack.transform, new Color(0.65f, 1f, 0.72f, 1f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        Stretch(progress.rectTransform, Vector2.zero, Vector2.zero);
    }

    private void UpdatePauseGestureMenu()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ResumeGame();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ExitToMenu();
        }

        if (gestureInput == null)
        {
            return;
        }

        SetProgress(pauseLeftProgress, gestureInput.GetHoldProgress(FinalGestureZone.Left, gestureHoldTime));
        SetProgress(pauseRightProgress, gestureInput.GetHoldProgress(FinalGestureZone.Right, gestureHoldTime));

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Left, gestureHoldTime))
        {
            ResumeGame();
        }

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Right, gestureHoldTime))
        {
            ExitToMenu();
        }
    }

    private void UpdateFinalGestureMenu()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            RestartGame();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ExitToMenu();
        }

        if (gestureInput == null)
        {
            return;
        }

        SetProgress(finalLeftProgress, gestureInput.GetHoldProgress(FinalGestureZone.Left, gestureHoldTime));
        SetProgress(finalRightProgress, gestureInput.GetHoldProgress(FinalGestureZone.Right, gestureHoldTime));

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Left, gestureHoldTime))
        {
            RestartGame();
        }

        if (gestureInput.ConsumeHeldGesture(FinalGestureZone.Right, gestureHoldTime))
        {
            ExitToMenu();
        }
    }

    private void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        Cursor.visible = paused;
        Cursor.lockState = CursorLockMode.None;

        if (playerController != null)
        {
            playerController.SetControlEnabled(!paused);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(paused);
        }

        SetProgress(pauseLeftProgress, 0f);
        SetProgress(pauseRightProgress, 0f);
    }

    private void ResumeGame()
    {
        SetPaused(false);
    }

    private void UpdateScoreView()
    {
        if (scoreText != null)
        {
            scoreText.text = score + " / " + coinCount;
        }

        if (bestScoreText != null)
        {
            bestScoreText.text = "Рекорд: " + highScore;
        }
    }

    private void ShowCenterMessage(string message, float duration)
    {
        if (centerMessageText == null)
        {
            return;
        }

        centerMessageText.text = message;
        CancelInvoke(nameof(HideCenterMessage));
        Invoke(nameof(HideCenterMessage), duration);
    }

    private void HideCenterMessage()
    {
        if (centerMessageText != null)
        {
            centerMessageText.text = "";
        }
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        return image;
    }

    private RawImage CreateRawImage(string objectName, Transform parent)
    {
        var imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<RawImage>();
        image.color = Color.white;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        var label = textObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    private void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void SetProgress(Image image, float value)
    {
        if (image != null)
        {
            image.fillAmount = value;
        }
    }

    private void CreateEventSystemIfNeeded()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
