// Автор: Марьяновский Владислав Андреевич

using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    public event Action<int, int> HealthChanged;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float NormalizedHealth => maxHealth <= 0 ? 0f : (float)currentHealth / maxHealth;

    private void Awake()
    {
        ClampValues();
    }

    private void OnEnable()
    {
        NotifyChanged();
    }

    private void OnValidate()
    {
        ClampValues();
    }

    public void Damage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetHealth(currentHealth - amount);
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetHealth(currentHealth + amount);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        NotifyChanged();
    }

    private void ClampValues()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private void NotifyChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
