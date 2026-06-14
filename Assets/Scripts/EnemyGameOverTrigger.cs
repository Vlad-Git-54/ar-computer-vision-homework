// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyGameOverTrigger : MonoBehaviour
{
    [SerializeField] private string playerObjectName = "Robot Player";
    [SerializeField] private float damagePercent = 0.1f;
    [SerializeField] private float damageCooldown = 0.75f;

    private float nextDamageTime;

    private void Awake()
    {
        var enemyCollider = GetComponent<Collider>();
        enemyCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var capsulePlayer = other.GetComponentInParent<CapsulePlayerController>();
        var robotPlayer = other.GetComponentInParent<RobotCoinPlayerController>();
        var playerRoot = GetPlayerRoot(other, capsulePlayer, robotPlayer);

        if (playerRoot == null)
        {
            return;
        }

        if (Time.time < nextDamageTime)
        {
            return;
        }

        nextDamageTime = Time.time + damageCooldown;

        var playerHealth = playerRoot.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = playerRoot.AddComponent<PlayerHealth>();
        }

        var damage = Mathf.CeilToInt(playerHealth.MaxHealth * damagePercent);
        playerHealth.Damage(damage);
        Debug.Log("Враг нанес урон: " + damage);

        var scoreHud = ScoreHudController.Instance;
        if (scoreHud == null)
        {
            scoreHud = FindObjectOfType<ScoreHudController>();
        }

        var droppedCoins = CoinCollectible.DropCollectedCoinsToField();
        if (droppedCoins > 0 && scoreHud != null)
        {
            scoreHud.ResetCurrentScore();
            Debug.Log("Монетки возвращены на поле: " + droppedCoins);
        }

        if (playerHealth.CurrentHealth > 0)
        {
            return;
        }

        if (scoreHud != null)
        {
            Debug.Log("Игра окончена. Здоровье игрока закончилось.");
            scoreHud.EndGame();
        }
    }

    private GameObject GetPlayerRoot(Collider other, CapsulePlayerController capsulePlayer, RobotCoinPlayerController robotPlayer)
    {
        if (robotPlayer != null)
        {
            return robotPlayer.gameObject;
        }

        if (capsulePlayer != null)
        {
            return capsulePlayer.gameObject;
        }

        var root = other.transform.root;
        if (root != null && root.name == playerObjectName)
        {
            return root.gameObject;
        }

        return null;
    }
}
