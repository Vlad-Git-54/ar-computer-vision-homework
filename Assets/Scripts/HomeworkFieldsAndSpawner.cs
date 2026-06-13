// Автор: Марьяновский Владислав Андреевич

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HomeworkSpawnMode
{
    Default,
    NearTarget,
    CustomPoint
}

public class HomeworkFieldsAndSpawner : MonoBehaviour
{
    [Header("Author")]
    [SerializeField] private string author = "Марьяновский Владислав Андреевич";

    [Header("Basic types")]
    [SerializeField] private bool boolValue = true;
    [SerializeField] private int intValue = 292405;
    [SerializeField] private float floatValue = 1.5f;
    [SerializeField] private double doubleValue = 2.0d;
    [SerializeField] private string stringValue = "Group 292405-1";
    [SerializeField] private Color colorValue = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private Vector2 vector2Value = new Vector2(2f, 1f);
    [SerializeField] private Vector3 vector3Value = new Vector3(0f, 0f, 2f);
    [SerializeField] private HomeworkSpawnMode enumValue = HomeworkSpawnMode.CustomPoint;

    [Header("Collection")]
    [SerializeField] private List<string> stringCollection = new List<string>
    {
        "Unity component",
        "Editor fields",
        "Prefab spawn"
    };

    [Header("References")]
    [SerializeField] private HomeworkDataTarget targetComponent;
    [SerializeField] private GameObject prefabToSpawn;

    [Header("Spawn settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 spawnRotation = new Vector3(0f, 180f, 0f);

    private bool prefabWasPlaced;

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        PlacePrefabOnScene();
    }

    private void PlacePrefabOnScene()
    {
        if (prefabWasPlaced || prefabToSpawn == null)
        {
            return;
        }

        var position = GetSpawnPosition();
        var rotation = Quaternion.Euler(spawnRotation);
        var placedPrefab = Instantiate(prefabToSpawn, position, rotation);

        placedPrefab.name = $"{prefabToSpawn.name} Placed After First Frame";
        prefabWasPlaced = true;
    }

    private Vector3 GetSpawnPosition()
    {
        if (enumValue == HomeworkSpawnMode.NearTarget && targetComponent != null)
        {
            return targetComponent.transform.position + Vector3.right * 1.5f;
        }

        if (enumValue == HomeworkSpawnMode.CustomPoint)
        {
            return spawnPosition;
        }

        return transform.position + Vector3.forward * 2f;
    }
}
