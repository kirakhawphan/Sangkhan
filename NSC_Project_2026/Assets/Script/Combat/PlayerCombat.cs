using UnityEngine;
using System;

public class PlayerCombat : MonoBehaviour
{
    [Header("Light Attack Settings")]
    [SerializeField] private int maxLightComboStep = 3;
    [SerializeField] private float lightAttackCooldown = 0.3f;
    [SerializeField] private float lightComboResetTime = 1.0f;
    [SerializeField] private float lightComboFinishCooldown = 1.5f;
    [Tooltip("ล็อคการเดินตอนต่อยธรรมดา")] [SerializeField] private float lightAttackLockTime = 0.3f;
    [Tooltip("ล็อคการเดินตอนจบคอมโบ")] [SerializeField] private float lightFinishAttackLockTime = 0.6f;

    [Header("Global Combat Settings")]
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("Miss Penalty")]
    [SerializeField] private float missRecoveryPenalty = 0.2f;

    [Header("Attack Dash")]
    [SerializeField] private AnimationCurve dashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private float dashDistance = 2f;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Block Settings")]
    [Tooltip("เปิด-ปิดระบบตั้งการ์ด (Block)")]
    public bool canBlock = true;

    [Header("Dodge Settings")]
    [Tooltip("เปิด-ปิดระบบหลบ (Dodge/Dash)")]
    public bool canDodge = true;
    [SerializeField] private KeyCode dodgeKey = KeyCode.Q;
    [SerializeField] private float dodgeDistance = 5f;
    [SerializeField] private float dodgeDuration = 0.3f;
    [SerializeField] private AnimationCurve dodgeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private Cooldown dodgeCooldown = new Cooldown { duration = 1.0f };

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
    private HealthSystem currentHealthSystem; // [เพิ่ม] สำหรับอัปเดตสถานะ Block

    // --- Timers ---
    private float currentBufferTimer;
    private float currentAttackCooldownTimer;
    private float currentMovementLockTimer;
    private float comboTimer;
    private float currentComboFinishCooldownTimer;

    // --- Dash State ---
    private bool isDashing;
    private float currentDashTime;

    // --- Dodge State ---
    public bool IsDodging => isDodging;
    private bool isDodging = false;
    private float currentDodgeTime = 0f;
    private Vector3 dodgeDirection;
    private float dodgeAnimationClipLength = 0.3f;
    private float originalAnimatorSpeed = 1f;
    private float dodgeCurveArea = 0.5f;

    // --- Combo State ---
    private int currentComboStep;
    private int currentComboPath; // เก็บประวัติคอมโบ เช่น เบา-เบา-หนัก = 112
    public int CurrentComboPath => currentComboPath; // เปิดให้อ่านค่า
    
    // [เพิ่ม] จดจำประวัติคอมโบในอดีต (เช่น 111, 112, 22) เพื่อให้ศัตรูนำไปประมวลผลต่อ
    public System.Collections.Generic.List<int> comboHistory = new System.Collections.Generic.List<int>();

    private bool wasAttacking; // สำหรับเช็คการเปลี่ยน state เพื่อยิง Event

    public bool IsAttacking => currentMovementLockTimer > 0f;

    // Zero GC: Cached Animator Hashes
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    private static readonly int HashComboPath = Animator.StringToHash("ComboPath"); // [นำกลับมา] ส่งรหัสคอมโบไป Animator เผื่อทำท่าสูตร
    private static readonly int HashAttack = Animator.StringToHash("Attack"); // Trigger ตีซ้าย
    private static readonly int HashHeavyAttack = Animator.StringToHash("HeavyAttack"); // Trigger ตีขวา
    private static readonly int HashIsBlocking = Animator.StringToHash("IsBlocking");
    private static readonly int HashDodge = Animator.StringToHash("Dash");

    private Playermovement playermovement;

    private void Awake()
    {
        currentAnimator = GetComponentInChildren<Animator>();
        currentWeaponHitboxes = GetComponentsInChildren<MeleeHitbox>();
        currentController = GetComponent<CharacterController>();
        currentBodyTransform = transform;
        currentSkillRunner = GetComponent<Skills.Core.SkillRunner>(); // [เพิ่ม] ดึงคอมโพเนนต์สกิล
        currentHealthSystem = GetComponent<HealthSystem>(); // [เพิ่ม] แคช HealthSystem
        playermovement = GetComponent<Playermovement>();

        dodgeKey = KeyCode.Q;
        CalculateDodgeCurveArea();

#if UNITY_EDITOR
        if (currentWeaponHitboxes == null || currentWeaponHitboxes.Length == 0)
        {
            Debug.LogWarning("[PlayerCombat] No MeleeHitbox found in children!");
        }
#endif
    }

    private void Start()
    {
        SyncDodgeDurationWithAnimation();
    }

    private void OnValidate()
    {
        CalculateDodgeCurveArea();
    }

    private void SyncDodgeDurationWithAnimation()
    {
        if (currentAnimator == null || currentAnimator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in currentAnimator.runtimeAnimatorController.animationClips)
        {
            string clipName = clip.name.ToLower();
            if (clipName.Contains("dash") || clipName.Contains("roll") || clipName.Contains("dodge"))
            {
                dodgeAnimationClipLength = clip.length;
                break;
            }
        }
    }

    private void CalculateDodgeCurveArea()
    {
        dodgeCurveArea = 0f;
        if (dodgeCurve == null)
        {
            dodgeCurveArea = 1f;
            return;
        }
        
        int steps = 20;
        for (int i = 0; i < steps; i++)
        {
            float t1 = (float)i / steps;
            float t2 = (float)(i + 1) / steps;
            float v1 = dodgeCurve.Evaluate(t1);
            float v2 = dodgeCurve.Evaluate(t2);
            dodgeCurveArea += (v1 + v2) / 2f * (1f / steps);
        }
        
        if (dodgeCurveArea <= 0.01f) dodgeCurveArea = 1f;
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
        currentComboPath = 0;
        currentBufferTimer = 0f;
        currentAttackCooldownTimer = 0f;
        currentMovementLockTimer = 0f;
        comboTimer = 0f;
        currentComboFinishCooldownTimer = 0f;
        isDashing = false;
        isDodging = false;
        wasAttacking = false;

        if (currentAnimator != null)
        {
            currentAnimator.SetInteger(HashComboStep, 0);
            currentAnimator.SetInteger(HashComboPath, 0);
        }
    }

    private void Update()
    {
        if (currentAnimator == null) return;

        float dt = Time.deltaTime; // Cache Time.deltaTime to reduce C++ interop overhead

        UpdateTimers(dt);
        UpdateCombatTargeting();
        HandleDash(dt);
        HandleDodgeMovement(dt);
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

    private void HandleDodgeMovement(float dt)
    {
        if (!isDodging) return;

        currentDodgeTime += dt;

        if (currentDodgeTime >= dodgeDuration)
        {
            isDodging = false;
            if (currentAnimator != null)
            {
                currentAnimator.speed = originalAnimatorSpeed; // รีเซ็ตความเร็วกลับเป็นปกติ
            }
            return;
        }

        float normalizedTime = currentDodgeTime / dodgeDuration;
        float force = dodgeCurve.Evaluate(normalizedTime);
        Vector3 dodgeMovement = dodgeDirection * ((dodgeDistance / dodgeDuration) * (force / dodgeCurveArea) * dt);

        if (currentController != null)
        {
            currentController.Move(dodgeMovement);
        }
    }

    private void HandleDodgeInput()
    {
        if (!canDodge) return;
        if (isDodging) return;

        // เช็ค Input กด Dodge, เช็คคูลดาวน์ และต้องเหยียบพื้นอยู่ (สมมติว่าเช็ค isGrounded จาก CharacterController)
        bool isGrounded = currentController != null ? currentController.isGrounded : true;

        if (Input.GetKeyDown(dodgeKey) && dodgeCooldown.IsReady() && isGrounded)
        {
            if (IsAttacking) return; // ห้ามพุ่งตอนโจมตี

            StartDodge();
        }
    }

    private void StartDodge()
    {
        isDodging = true;
        currentDodgeTime = 0f;
        dodgeCooldown.StartCooldown();

        // ปรับความเร็วของ Animator ให้แอนิเมชันเล่นจบพร้อมกับ dodgeDuration พอดี
        if (currentAnimator != null)
        {
            originalAnimatorSpeed = currentAnimator.speed;
            if (dodgeDuration > 0f && dodgeAnimationClipLength > 0f)
            {
                currentAnimator.speed = dodgeAnimationClipLength / dodgeDuration;
            }
        }

        // คำนวณทิศทางพุ่งจาก Input WASD
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        if (inputDir.sqrMagnitude >= 0.01f)
        {
            float cameraAngle = playermovement != null ? playermovement.GetCameraAngle() : currentBodyTransform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraAngle;
            dodgeDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            currentBodyTransform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
        }
        else
        {
            dodgeDirection = currentBodyTransform.forward;
        }

        // เปิด I-Frame ถ้ามี HealthSystem
        if (currentHealthSystem != null)
        {
            currentHealthSystem.ActivateIFrame();
        }

        // ทริกเกอร์แอนิเมชัน
        if (currentAnimator != null)
        {
            currentAnimator.SetTrigger(HashDodge);
        }
    }

    private void HandleInput()
    {
        // จัดการสถานะตั้งการ์ด (Block) - กดปุ่ม F ค้าง
        bool blockInput = canBlock && Input.GetKey(KeyCode.F);
        
        // เงื่อนไข: จะตั้งการ์ดได้ต้องไม่กำลังโจมตี และไม่พุ่งตัวอยู่
        bool isBlocking = blockInput && !IsAttacking && !isDashing && !isDodging;

        if (canBlock && currentAnimator != null)
        {
            currentAnimator.SetBool(HashIsBlocking, isBlocking);
        }

        if (currentHealthSystem != null)
        {
            currentHealthSystem.IsBlocking = isBlocking;
        }

        // Handle Dodge Input
        // (เฉพาะตอนที่เราสิงร่างอยู่เท่านั้น ถ้ามีตัวจัดการ Possession ให้เช็คที่นี่)
        bool isPossessed = playermovement != null ? playermovement.isPossessed : true;
        if (isPossessed)
        {
            HandleDodgeInput();
        }

        // สามารถต่อยอดไปใช้ Input System แทนได้โดยเรียกฟังก์ชัน BufferAttack() จากภายนอก
        if (isPossessed && Input.GetMouseButtonDown(0))
        {
            // ถ้ากำลังตั้งการ์ดหรือหลบอยู่ ห้ามโจมตี
            if (!isBlocking && !isDodging)
            {
                BufferAttack();
            }
        }

        // [เพิ่ม] คลิกขวาโจมตีหนัก (สามารถร่ายได้ทุกสเต็ป เพื่อใช้เป็นท่าปิดคอมโบ)
        if (isPossessed && Input.GetMouseButtonDown(1))
        {
            if (!isBlocking && !isDodging)
            {
                BufferHeavyAttack();
            }
        }
    }

    /// <summary>
    /// เปิด public เพื่อรองรับการเรียกจาก Input System (Decoupling)
    /// </summary>
    public void BufferAttack()
    {
        currentBufferTimer = inputBufferTime;
        isHeavyAttackingBuffer = false;
    }

    public void BufferHeavyAttack()
    {
        currentBufferTimer = inputBufferTime;
        isHeavyAttackingBuffer = true;
    }

    private void ProcessInputBuffer()
    {
        if (currentBufferTimer <= 0f) return;
        if (currentAttackCooldownTimer > 0f) return;
        if (currentComboFinishCooldownTimer > 0f) return;
        if (!IsGrounded()) return;

        ExecuteAttack(isHeavyAttackingBuffer);
    }

    private void ExecuteAttack(bool isHeavy)
    {
        currentBufferTimer = 0f;
        isHeavyAttackingBuffer = false; // เคลียร์ buffer

        int maxStep = isHeavy ? maxHeavyComboStep : maxLightComboStep;

        if (currentComboStep >= maxStep)
        {
            // บันทึกประวัติก่อนเริ่มคอมโบใหม่ (ถ้ามีการกดโจมตีต่อเนื่องโดยไม่รอ timeout)
            if (currentComboPath > 0)
            {
                comboHistory.Add(currentComboPath);
                if (comboHistory.Count > 10) comboHistory.RemoveAt(0);
            }
            currentComboStep = 0;
            currentComboPath = 0;
        }

        isDashing = true;
        currentDashTime = 0f;

        RotateTowardsTarget();

        currentComboStep++;
        // 1 = ตีเบา, 2 = ตีหนัก
        currentComboPath = (currentComboPath * 10) + (isHeavy ? 2 : 1);

        // ส่ง ComboStep เผื่อเอาไว้ใช้ (แต่ตัวหลักคือ Trigger)
        currentAnimator.SetInteger(HashComboStep, currentComboStep);
        
        // [นำกลับมา] ส่ง ComboPath เผื่อเอาไว้ทำ "ท่าพิเศษตามสูตร" จาก AnyState
        currentAnimator.SetInteger(HashComboPath, currentComboPath);

        // แยก Trigger ตามคลิกซ้าย/ขวา (สไตล์ DMC State-to-State)
        if (isHeavy)
        {
            currentAnimator.SetTrigger(HashHeavyAttack);
        }
        else
        {
            currentAnimator.SetTrigger(HashAttack);
        }

        if (currentComboStep >= maxStep)
        {
            currentComboFinishCooldownTimer = lightComboFinishCooldown;
            currentMovementLockTimer = isHeavy ? heavyAttackLockTime : lightFinishAttackLockTime; 
            comboTimer = lightComboFinishCooldown; 
            
            // จบคอมโบสมบูรณ์ บันทึกลงประวัติทันที
            comboHistory.Add(currentComboPath);
            if (comboHistory.Count > 10) comboHistory.RemoveAt(0);
        }
        else
        {
            currentAttackCooldownTimer = isHeavy ? heavyAttackCooldown : lightAttackCooldown;
            currentMovementLockTimer = isHeavy ? heavyAttackLockTime : lightAttackLockTime;
            comboTimer = lightComboResetTime;
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

        // ถือว่าจบคอมโบเมื่อถึง Max ของฝั่งไหนก็ตาม (กันบั๊กกรณีสลับไปมา)
        bool isMaxComboFinish = (currentComboStep >= maxLightComboStep) || (currentComboStep >= maxHeavyComboStep);

        // [เพิ่ม] บันทึกประวัติก่อนรีเซ็ตเมื่อเกิด Timeout
        if (currentComboPath > 0 && !comboHistory.Contains(currentComboPath) && currentComboStep < maxLightComboStep && currentComboStep < maxHeavyComboStep)
        {
            comboHistory.Add(currentComboPath);
            if (comboHistory.Count > 10) comboHistory.RemoveAt(0);
        }

        currentComboStep = 0;
        currentComboPath = 0;
        currentAnimator.SetInteger(HashComboStep, 0);
        currentAnimator.SetInteger(HashComboPath, 0); // รีเซ็ตสูตร

        if (!isMaxComboFinish)
        {
            currentComboFinishCooldownTimer = lightComboFinishCooldown;
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

    // --- Heavy Attack ---
    private bool isHeavyAttackingBuffer;
    [Header("Heavy Attack Settings")]
    [SerializeField] private int maxHeavyComboStep = 3;
    [SerializeField] private float heavyAttackCooldown = 1.0f;
    [SerializeField] private float heavyAttackLockTime = 0.8f;
    [SerializeField] private float heavyDamageMultiplier = 2.0f;

    // [เพิ่ม] ลิสต์กันซ้ำที่ใช้ร่วมกันระหว่าง Hitbox ทุกตัว (จองครั้งเดียว Zero GC)
    private readonly System.Collections.Generic.List<IDamageable> sharedDamagedTargets = new System.Collections.Generic.List<IDamageable>(10);

    public void AE_TriggerWeaponAttack()
    {
        ExecuteHitboxes(1f);
    }

    public void AE_TriggerHeavyAttack()
    {
        ExecuteHitboxes(heavyDamageMultiplier);
    }

    private void ExecuteHitboxes(float damageMultiplier)
    {
        if (currentWeaponHitboxes == null || currentWeaponHitboxes.Length == 0) return;

        bool anyHit = false;
        sharedDamagedTargets.Clear();

        // Zero GC: for loop แทน foreach
        for (int i = 0; i < currentWeaponHitboxes.Length; i++)
        {
            if (currentWeaponHitboxes[i] != null)
            {
                if (currentWeaponHitboxes[i].PerformAttack(sharedDamagedTargets, damageMultiplier))
                {
                    anyHit = true;
                }
            }
        }

        if (!anyHit)
        {
            currentMovementLockTimer += missRecoveryPenalty;
            currentAttackCooldownTimer += missRecoveryPenalty;
            
            if (currentComboStep >= maxLightComboStep || currentComboStep >= maxHeavyComboStep)
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
