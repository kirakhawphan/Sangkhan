using UnityEngine;

[CreateAssetMenu(fileName = "New EnemyData", menuName = "Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Unknown Enemy";

    [Header("Health & Poise")]
    public float maxHealth = 100f;
    public float maxPoise = 20f;

    [Header("Movement")]
    public float idleSpeed = 3.5f;
    public float chaseSpeed = 6f;
    public float strafeSpeed = 2f;
    public float stoppingDistance = 1f;
    public float rotationSpeed = 20f;
    public bool keepDistance = false;
    public float retreatDistance = 2.5f;
    public float retreatMultiplier = 2f;

    [Header("Combat")]
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    public int comboStep = 1;
    public float normalAttackLockTime = 0.5f;
    public float finishAttackLockTime = 1f;
    public float attackAnimationTime = 1.2f;

    [Header("AI Behavior")]
    public float stunDuration = 0.8f;
    public float circleRadius = 8f;
    public float tooCloseDistance = 2.5f;
    public float retryInterval = 1.2f;
    public float strafeChangeDuration = 2.5f;

    [Header("Detection")]
    public float detectionRange = 10f;
    public float aimRadius = 1.5f;
}
