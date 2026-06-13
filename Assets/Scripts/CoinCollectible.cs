// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class CoinCollectible : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CapsulePlayerController>() == null)
        {
            return;
        }

        Debug.Log("Монетка собрана!");
        Destroy(gameObject);
    }
}
