// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

public class HomeworkDataTarget : MonoBehaviour
{
    [SerializeField] private string targetName = "Target component for homework";
    [SerializeField] private int targetNumber = 292405;

    public string TargetName => targetName;
    public int TargetNumber => targetNumber;
}
