// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class CoinCollectible : MonoBehaviour
{
    [SerializeField] private string pickupSoundResourcePath = "Audio/CoinPickup";
    [SerializeField] private float pickupSoundVolume = 0.8f;

    private static AudioClip cachedPickupSound;

    private void OnTriggerEnter(Collider other)
    {
        var capsulePlayer = other.GetComponentInParent<CapsulePlayerController>();
        var robotPlayer = other.GetComponentInParent<RobotCoinPlayerController>();

        if (capsulePlayer == null && robotPlayer == null)
        {
            return;
        }

        Debug.Log("Монетка собрана!");
        PlayPickupSound();
        Destroy(gameObject);
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
