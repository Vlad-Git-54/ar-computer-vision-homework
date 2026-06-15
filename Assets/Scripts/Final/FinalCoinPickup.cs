// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinalCoinPickup : MonoBehaviour
{
    private FinalGameController gameController;
    private bool collected;

    public void Configure(FinalGameController owner)
    {
        gameController = owner;
    }

    public void ResetPickup()
    {
        collected = false;
        gameObject.SetActive(true);
    }

    private void Awake()
    {
        var coinCollider = GetComponent<Collider>();
        coinCollider.isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other.GetComponentInParent<FinalPlayerController>() == null)
        {
            return;
        }

        collected = true;
        if (gameController != null)
        {
            gameController.CollectCoin(transform.position);
        }

        gameObject.SetActive(false);
    }
}
