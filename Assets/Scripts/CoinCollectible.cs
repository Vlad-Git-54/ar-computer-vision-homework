// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class CoinCollectible : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var capsulePlayer = other.GetComponentInParent<CapsulePlayerController>();
        var robotPlayer = other.GetComponentInParent<RobotCoinPlayerController>();

        if (capsulePlayer == null && robotPlayer == null)
        {
            return;
        }

        Debug.Log("Монетка собрана!");
        Destroy(gameObject);
    }
}
