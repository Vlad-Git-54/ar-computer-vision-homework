// Автор: Марьяновский Владислав Андреевич

using System.Collections.Generic;
using UnityEngine;

public class CoinCollectible : MonoBehaviour
{
    [SerializeField] private int scoreValue = 1;
    [SerializeField] private string pickupSoundResourcePath = "Audio/CoinPickup";
    [SerializeField] private float pickupSoundVolume = 0.8f;
    [SerializeField] private float dropHeight = 0.45f;

    private static readonly List<CoinCollectible> collectedCoins = new List<CoinCollectible>();
    private static AudioClip cachedPickupSound;
    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected)
        {
            return;
        }

        var capsulePlayer = other.GetComponentInParent<CapsulePlayerController>();
        var robotPlayer = other.GetComponentInParent<RobotCoinPlayerController>();

        if (capsulePlayer == null && robotPlayer == null)
        {
            return;
        }

        Debug.Log("Монетка собрана!");
        isCollected = true;
        AddToCollectedCoins();
        AddScore();
        PlayPickupSound();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        collectedCoins.Remove(this);
    }

    public static int DropCollectedCoinsToField()
    {
        var coinsToDrop = new List<CoinCollectible>();
        foreach (var coin in collectedCoins)
        {
            if (coin != null)
            {
                coinsToDrop.Add(coin);
            }
        }

        collectedCoins.Clear();

        for (var i = 0; i < coinsToDrop.Count; i++)
        {
            coinsToDrop[i].DropToField(i, coinsToDrop.Count);
        }

        return coinsToDrop.Count;
    }

    private void AddToCollectedCoins()
    {
        if (!collectedCoins.Contains(this))
        {
            collectedCoins.Add(this);
        }
    }

    private void DropToField(int index, int totalCount)
    {
        isCollected = false;
        transform.position = CreateDropPosition(index, totalCount);
        transform.rotation = Quaternion.Euler(0f, index * 31f, 0f);
        gameObject.SetActive(true);
    }

    private Vector3 CreateDropPosition(int index, int totalCount)
    {
        var angle = index * 137.5f * Mathf.Deg2Rad;
        var radius = 3.2f + index % 6 * 1.15f + totalCount * 0.03f;
        var x = Mathf.Clamp(Mathf.Cos(angle) * radius, -10.5f, 10.5f);
        var z = Mathf.Clamp(Mathf.Sin(angle) * radius, -10.5f, 10.5f);
        return new Vector3(x, dropHeight, z);
    }

    private void AddScore()
    {
        var scoreHud = ScoreHudController.Instance;
        if (scoreHud == null)
        {
            scoreHud = FindObjectOfType<ScoreHudController>();
        }

        if (scoreHud != null)
        {
            scoreHud.AddScore(scoreValue);
        }
    }

    private void PlayPickupSound()
    {
        if (cachedPickupSound == null)
        {
            cachedPickupSound = Resources.Load<AudioClip>(pickupSoundResourcePath);
        }

        if (cachedPickupSound != null)
        {
            AudioSource.PlayClipAtPoint(cachedPickupSound, transform.position, pickupSoundVolume);
        }
    }
}
