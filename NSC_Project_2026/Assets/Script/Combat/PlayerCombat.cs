using UnityEngine;
using System;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("Cooldowns")]
    [SerializeField] private float attackCooldown = 0.3f;
    [SerializeField] private float comboResetTime = 1.0f;
    [SerializeField] private float comboFinishCooldown = 1.5f;

    [Header("Combo Window Options")]
    [Tooltip("ผู้เล่นสามารถกดตีครั้งถัดไปล่วงหน้าได้นานแค่ไหน")] [SerializeField] private float normalAttackLockTime = 0.3f;
    [SerializeField] private float finishAttackLockTime = 0.6f;

    [Header("Miss Penalty")]
    [SerializeField] private float missRecoveryPenalty = 0.2f;

    [Header("Attack Dash")]
    [SerializeField] private AnimationCurve dashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private float dashDistance = 2f;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Combat Targeting (Soft Lock-on)")]
    [SerializeField] private TargetDetector targetDetector;
    [SerializeField] private Transform aimOrigin;

    // [เพิ่ม] เปิดให้สคริปต์อื่น (เช่น GrappleSkillEffect) เข้าถึง TargetDetector ได้
    public TargetDetector GetTargetDetector() => targetDetector;

    // --- Events for Decoupling ---
    public event Action OnAttackExecuted;
    public event Action<bool> OnAttackStateChanged;

    // --- Component Caches ---
    private MeleeHitbox[] currentWeaponHitboxes;
    private Animator currentAnimator;
    private CharacterController currentController;
    private Transform currentBodyTransform;
    private Skills.Core.SkillRunner currentSkillRunner; // [เพิ่ม] ระบบสกิล

    // --- Timers ---
    private float currentBufferTimer;
    private float currentAttackCooldownTimer;
    private float currentMovementLockTimer;
    private float comboTimer;
    private float currentComboFinishCooldownTimer;

    // --- Dash State ---
    private bool isDashing;
    private float currentDashTime;

    // --- Combo State ---
    private int currentComboStep;
    private bool wasAttacking; // สำหรับเช็คการเปลี่ยน state เพื่อยิง Event

    public bool IsAttacking => currentMovementLockTimer > 0f;

    // Zero GC: Cached Animator Hashes
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    private static readonly int HashAttack = Animator.StringToHash("Attack");

    private void Awake()
    {
        currentAnimator = GetComponentInChildren<Animator>();
        currentWeaponHitboxes = GetComponentsInChildren<MeleeHitbox>();
        currentController = GetComponent<CharacterController>();
        currentBodyTransform = transform;
        currentSkillRunner = GetComponent<Skills.Core.SkillRunner>(); // [เพิ่ม] ดึงคอมโพเนนต์สกิล

#if UNITY_EDITOR
        if (currentWeaponHitboxes == null || currentWeaponHitboxes.Length == 0)
        {
            Debug.LogWarning("[PlayerCombat] No MeleeHitbox found in children!");
        }
#endif
    }

    private void OnEnable()
    {
        ResetCombatState();
    }

    private void OnDisable()
    {
        ResetCombatState();
    }

    private void ResetCombatState()
    {
        currentComboStep = 0;
        currentBufferTimer = 0f;
        currentAttackCooldownTimer = 0f;
        currentMovementLockTimer = 0f;
        comboTimer = 0f;
        currentComboFinishCooldownTimer = 0f;
        isDashing = false;
        wasAttacking = false;

        if (currentAnimator != null)
        {
            currentAnimator.SetInteger(HashComboStep, 0);
        }
    }

    private void Update()
    {
        if (currentAnimator == null) return;

        float dt = Time.deltaTime; // Cache Time.deltaTime to reduce C++ interop overhead

        UpdateTimers(dt);
        UpdateCombatTargeting();
        HandleDash(dt);
        HandleInput();
        ProcessInputBuffer();
        CheckComboTimeout();
        CheckAttackStateChange();
    }

    private void UpdateCombatTargeting()
    {
        if (targetDetector == null || aimOrigin == null || currentBodyTransform == null) return;
        
        targetDetector.UpdateDetection(aimOrigin, aimOrigin.forward, currentBodyTransform);
    }

    private void UpdateTimers(float dt)
    {
        if (currentBufferTimer > 0f) currentBufferTimer -= dt;
        if (currentAttackCooldownTimer > 0f) currentAttackCooldownTimer -= dt;
        if (currentMovementLockTimer > 0f) currentMovementLockTimer -= dt;
        if (comboTimer > 0f) comboTimer -= dt;
        if (currentComboFinishCooldownTimer > 0f) currentComboFinishCooldownTimer -= dt;
    }

    private void HandleDash(float dt)
    {
        if (!isDashing) return;

        currentDashTime += dt;

        if (currentDashTime >= dashDuration)
        {
            isDashing = false;
            return;
        }

        float normalizedTime = currentDashTime / dashDuration;
        float force = dashCurve.Evaluate(normalizedTime);
        Vector3 dashMovement = currentBodyTransform.forward * ((dashDistance / dashDuration) * force * dt);

        if (currentController != null)
        {
            currentController.Move(dashMovement);
        }
    }

    private void HandleInput()
    {
        // สามารถต่อยอดไปใช้ Input System แทนได้โดยเรียกฟังก์ชัน BufferAttack() จากภายนอก
        if (Input.GetMouseButtonDown(0))
        {
            BufferAttack();
        }
    }

    /// <summary>
    /// เปิด public เพื่อรองรับการเรียกจาก Input System (Decoupling)
    /// </summary>
    public void BufferAttack()
    {
        currentBufferTimer = inputBufferTime;
    }

    private void ProcessInputBuffer()
    {
        if (currentBufferTimer <= 0f) return;
        if (currentAttackCooldownTimer > 0f) return;
        if (currentComboFinishCooldownTimer > 0f) return;
        if (!IsGrounded()) return;

        ExecuteAttack();
    }

    private void ExecuteAttack()
    {
        currentBufferTimer = 0f;

        if (currentComboStep >= maxComboStep)
        {
            currentComboStep = 0;
        }

        isDashing = true;
        currentDashTime = 0f;

        RotateTowardsTarget();

        currentComboStep++;

        currentAnimator.SetInteger(HashComboStep, currentComboStep);
        currentAnimator.SetTrigger(HashAttack);

        if (currentComboStep >= maxComboStep)
        {
            currentComboFinishCooldownTimer = comboFinishCooldown;
            currentMovementLockTimer = finishAttackLockTime; 
            comboTimer = comboFinishCooldown; 
        }
        else
        {
            currentAttackCooldownTimer = attackCooldown;
            currentMovementLockTimer = normalAttackLockTime;
            comboTimer = comboResetTime;
        }

        // Trigger Event สำหรับสคริปต์ภายนอก
        OnAttackExecuted?.Invoke();
    }

    /// <summary>
    /// [เพิ่ม] ใช้ล็อคการเดินจากระบบอื่น (เช่น SkillRunner)
    /// </summary>
    public void LockMovementForSkill(float duration)
    {
        if (currentMovementLockTimer < duration)
        {
            currentMovementLockTimer = duration;
        }
    }

    private void RotateTowardsTarget()
    {
        if (targetDetector == null || targetDetector.CurrentTarget == null || currentBodyTransform == null) return;

        Vector3 directionToTarget = targetDetector.CurrentTarget.transform.position - currentBodyTransform.position;
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            currentBodyTransform.rotation = Quaternion.LookRotation(directionToTarget);
        }
    }

    private void CheckComboTimeout()
    {
        if (currentComboStep == 0) return;
        if (comboTimer > 0f) return;

        bool isMaxComboFinish = (currentComboStep >= maxComboStep);

        currentComboStep = 0;
        currentAnimator.SetInteger(HashComboStep, 0);

        if (!isMaxComboFinish)
        {
            currentComboFinishCooldownTimer = comboFinishCooldown;
        }
    }

    private void CheckAttackStateChange()
    {
        bool isCurrentlyAttacking = IsAttacking;
        if (wasAttacking != isCurrentlyAttacking)
        {
            wasAttacking = isCurrentlyAttacking;
            OnAttackStateChanged?.Invoke(isCurrentlyAttacking);
        }
    }

    public void AE_TriggerWeaponAttack()
    {
        if (currentWeaponHitboxes == null || currentWeaponHitboxes.Length == 0) return;

        bool anyHit = false;

        // Zero GC: for loop แทน foreach
        for (int i = 0; i < currentWeaponHitboxes.Length; i++)
        {
            if (currentWeaponHitboxes[i] != null)
            {
                if (currentWeaponHitboxes[i].PerformAttack())
                {
                    anyHit = true;
                }
            }
        }

        if (!anyHit)
        {
            currentMovementLockTimer += missRecoveryPenalty;
            currentAttackCooldownTimer += missRecoveryPenalty;
            
            if (currentComboStep >= maxComboStep)
            {
                currentComboFinishCooldownTimer += missRecoveryPenalty;
            }
        }
    }

    private bool IsGrounded()
    {
        if (currentController != null) return currentController.isGrounded;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (targetDetector != null)
        {
            Vector3 originPos = aimOrigin != null ? aimOrigin.position : transform.position;
            Vector3 forwardDir = aimOrigin != null ? aimOrigin.forward : transform.forward;
            targetDetector.DrawGizmos(originPos, forwardDir);
        }
    }
}
