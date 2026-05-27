using System.Collections;
using UnityEngine;

// ระบบเอฟเฟกต์ FOV (Field of View) ของกล้อง — ปรับแต่งได้จาก Inspector ว่า DamageType ไหนจะ trigger
// เช่น ซูมเข้าเมื่อโดนโจมตี, ซูมออกเมื่อวิ่ง, ยืดหดตอน Dash, เปิดกว้างตอนเล็ง ฯลฯ
//
// หลักการ: ควบคุม Camera.fieldOfView โดยตรง มีระบบ Layer (ชั้นเอฟเฟกต์)
// สามารถ Subscribe OnDamageTaken ได้เหมือน CameraShake + เรียกใช้ผ่าน Public API ได้อิสระ
//
// แปะสคริปต์นี้ไว้ที่ Main Camera หรือ GameObject ใดก็ได้
public class CameraFOV : MonoBehaviour
{
    public static CameraFOV Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==================== Enum สำหรับเลือก Easing Curve ====================

    public enum EaseType
    {
        Linear,       // เปลี่ยนค่าสม่ำเสมอ
        EaseInOut,    // ช้า → เร็ว → ช้า (SmoothStep)
        EaseIn,       // เริ่มช้าแล้วเร่ง
        EaseOut,      // เริ่มเร็วแล้วชะลอ
        Punch,        // กระชากไปแล้วเด้งกลับ (เหมาะกับโดนตี)
        Elastic       // สปริงเด้งหลายรอบ (เหมาะกับ Power-Up)
    }

    // ==================== Inspector Settings ====================

    [Header("Camera Reference")]
    [Tooltip("กล้องที่ต้องการควบคุม FOV (ถ้าไม่ใส่จะหา Camera.main ให้อัตโนมัติ)")]
    [SerializeField] private Camera targetCamera;

    [Header("Base FOV")]
    [Tooltip("ค่า FOV ปกติที่กล้องจะกลับไปเสมอหลังเอฟเฟกต์จบ")]
    [SerializeField] private float baseFOV = 60f;

    [Header("Default FOV Kick Settings")]
    [Tooltip("ค่า FOV เริ่มต้นที่เปลี่ยนเมื่อโดนตี (ใช้เมื่อไม่มี ImpactProfile)")]
    [SerializeField] private float defaultFOVKick = -5f;

    [Tooltip("ระยะเวลาเริ่มต้นของเอฟเฟกต์ (วินาที)")]
    [SerializeField] private float defaultDuration = 0.25f;

    [Tooltip("รูปแบบ Easing เริ่มต้น")]
    [SerializeField] private EaseType defaultEase = EaseType.Punch;

    [Header("Sprint FOV (ตอนวิ่ง)")]
    [Tooltip("ค่า FOV ที่เพิ่มขึ้นเมื่อวิ่ง Sprint")]
    [SerializeField] private float sprintFOVBoost = 10f;

    [Tooltip("ความเร็วในการ Lerp FOV เข้า/ออกจากโหมดวิ่ง")]
    [SerializeField] private float sprintLerpSpeed = 8f;

    [Header("References")]
    [Tooltip("HealthSystem ของตัวละครที่ต้องการติดตาม (ลากมาใส่)")]
    [SerializeField] private HealthSystem targetHealthSystem;

    // ==================== Internal State ====================

    private Coroutine fovCoroutine;       // เอฟเฟกต์ FOV ชั่วคราว (Kick, Pulse)
    private bool isSprinting = false;     // สถานะวิ่ง Sprint
    private float sprintFOVCurrent = 0f;  // ค่า FOV ที่เพิ่มจากการวิ่ง (Lerp อยู่)
    private float kickFOVCurrent = 0f;    // ค่า FOV ที่เพิ่มจากเอฟเฟกต์ Kick ปัจจุบัน

    // ==================== Unity Lifecycle ====================

    private void Start()
    {
        // หา Camera อัตโนมัติถ้าไม่ได้ลากใส่ Inspector
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                Debug.LogError("[CameraFOV] ไม่พบกล้อง! กรุณาลาก Camera มาใส่ช่อง Inspector", this);
        }

        // เก็บค่า FOV เริ่มต้นจากกล้องจริง (ป้องกันค่าเริ่มต้นไม่ตรง)
        if (targetCamera != null)
        {
            baseFOV = targetCamera.fieldOfView;
        }
    }

    private void OnEnable()
    {
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken -= HandleDamageTaken;
        }
    }

    private void Update()
    {
        // Lerp ค่า Sprint FOV แบบนุ่มนวลทุกเฟรม
        float targetSprintFOV = isSprinting ? sprintFOVBoost : 0f;
        sprintFOVCurrent = Mathf.Lerp(sprintFOVCurrent, targetSprintFOV, sprintLerpSpeed * Time.deltaTime);

        // รวมค่า FOV ทั้งหมดแล้วเซ็ตให้กล้อง
        ApplyFOV();
    }

    private void ApplyFOV()
    {
        if (targetCamera == null) return;
        targetCamera.fieldOfView = baseFOV + sprintFOVCurrent + kickFOVCurrent;
    }

    // ==================== Public API สำหรับระบบสิงร่าง ====================

    /// <summary>
    /// สลับเป้าหมายไปยังร่างใหม่ (เรียกจาก PossessionManager เมื่อสิงร่าง)
    /// </summary>
    public void SetTarget(HealthSystem newHealthSystem)
    {
        // หยุดเอฟเฟกต์ค้างของร่างเก่า
        if (fovCoroutine != null)
        {
            StopCoroutine(fovCoroutine);
            fovCoroutine = null;
        }

        // รีเซ็ต Kick FOV ให้เป็น 0
        kickFOVCurrent = 0f;

        // --- Unsubscribe Event จากตัวเก่า ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken -= HandleDamageTaken;
        }

        // --- เปลี่ยนเป้าหมาย ---
        targetHealthSystem = newHealthSystem;

        // --- Subscribe Event ตัวใหม่ ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken += HandleDamageTaken;
        }
    }

    /// <summary>
    /// เปลี่ยนกล้องที่ควบคุม (กรณีมีหลายกล้องหรือสลับกล้องกลางเกม)
    /// </summary>
    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        if (targetCamera != null)
        {
            baseFOV = targetCamera.fieldOfView;
        }
    }

    // ==================== Public API สำหรับ Sprint ====================

    /// <summary>
    /// เปิด/ปิดเอฟเฟกต์ FOV ตอนวิ่ง Sprint (เรียกจาก Playermovement หรือ InputSystem)
    /// </summary>
    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
    }

    /// <summary>
    /// เปิด/ปิดเอฟเฟกต์ FOV ตอนวิ่ง Sprint พร้อมกำหนดค่า FOV เพิ่มเอง
    /// </summary>
    public void SetSprinting(bool sprinting, float customBoost)
    {
        isSprinting = sprinting;
        sprintFOVBoost = customBoost;
    }

    // ==================== Public API สำหรับ FOV Kick ====================

    /// <summary>
    /// สั่งเอฟเฟกต์ FOV Kick ด้วยค่าเริ่มต้นจาก Inspector
    /// </summary>
    public void TriggerKick()
    {
        TriggerKick(defaultFOVKick, defaultDuration, defaultEase);
    }

    /// <summary>
    /// สั่งเอฟเฟกต์ FOV Kick พร้อมกำหนดค่าเอง (ใช้ได้หลากหลาย เช่น Dash, โดนตี, ชาร์จพลัง ฯลฯ)
    /// </summary>
    /// <param name="fovChange">ค่า FOV ที่เปลี่ยน (ลบ = ซูมเข้า, บวก = ซูมออก)</param>
    /// <param name="duration">ระยะเวลาเอฟเฟกต์ (วินาที)</param>
    /// <param name="ease">รูปแบบ Easing</param>
    public void TriggerKick(float fovChange, float duration, EaseType ease = EaseType.Punch)
    {
        if (targetCamera == null) return;

        if (fovCoroutine != null)
        {
            StopCoroutine(fovCoroutine);
        }

        fovCoroutine = StartCoroutine(FOVKickRoutine(fovChange, duration, ease));
    }

    // ==================== Public API สำหรับ Smooth Transition ====================

    /// <summary>
    /// เปลี่ยน FOV แบบ Smooth ไปยังค่าเป้าหมาย (ไม่กลับค่าเดิมอัตโนมัติ)
    /// เหมาะสำหรับ: เล็งปืน (ADS), เปลี่ยนโหมดกล้อง, Cinematic Zoom
    /// </summary>
    /// <param name="targetFOV">ค่า FOV ปลายทาง</param>
    /// <param name="duration">ระยะเวลาที่ใช้เปลี่ยน (วินาที)</param>
    /// <param name="ease">รูปแบบ Easing</param>
    public void TransitionTo(float targetFOV, float duration, EaseType ease = EaseType.EaseInOut)
    {
        if (targetCamera == null) return;

        if (fovCoroutine != null)
        {
            StopCoroutine(fovCoroutine);
        }

        fovCoroutine = StartCoroutine(FOVTransitionRoutine(targetFOV, duration, ease));
    }

    /// <summary>
    /// เปลี่ยน FOV กลับไปค่า baseFOV แบบ Smooth
    /// </summary>
    public void TransitionToBase(float duration, EaseType ease = EaseType.EaseInOut)
    {
        TransitionTo(baseFOV, duration, ease);
    }

    // ==================== Public API สำหรับ Pulse ====================

    /// <summary>
    /// สั่งเอฟเฟกต์ FOV Pulse (ยืดออก → หดกลับ) ซ้ำหลายรอบ
    /// เหมาะสำหรับ: หัวใจเต้น, เลือดน้อย, ชาร์จพลัง
    /// </summary>
    /// <param name="fovChange">ค่า FOV ที่เปลี่ยนต่อรอบ</param>
    /// <param name="pulseDuration">ระยะเวลาต่อรอบ (วินาที)</param>
    /// <param name="pulseCount">จำนวนรอบ</param>
    public void TriggerPulse(float fovChange, float pulseDuration, int pulseCount = 3)
    {
        if (targetCamera == null) return;

        if (fovCoroutine != null)
        {
            StopCoroutine(fovCoroutine);
        }

        fovCoroutine = StartCoroutine(FOVPulseRoutine(fovChange, pulseDuration, pulseCount));
    }

    // ==================== Public API สำหรับตั้งค่า Base FOV ====================

    /// <summary>
    /// เปลี่ยนค่า Base FOV (ค่าปกติที่กลับไปหลังเอฟเฟกต์จบ) ทันที
    /// </summary>
    public void SetBaseFOV(float newBaseFOV)
    {
        baseFOV = newBaseFOV;
    }

    /// <summary>
    /// ดึงค่า Base FOV ปัจจุบัน
    /// </summary>
    public float GetBaseFOV()
    {
        return baseFOV;
    }
    // ==================== Event Handler ====================

    private void HandleDamageTaken(DamageInfo info)
    {
        if (info.impactProfile != null)
        {
            if (info.impactProfile.enableReceiverFOVKick)
            {
#if UNITY_EDITOR
                Debug.Log($"[CameraFOV] Receiver FOV Kick triggered via profile. Amount: {info.impactProfile.receiverFOVKickAmount}, Duration: {info.impactProfile.receiverFOVKickDuration}");
#endif
                TriggerKick(
                    info.impactProfile.receiverFOVKickAmount, 
                    info.impactProfile.receiverFOVKickDuration, 
                    info.impactProfile.receiverFOVKickEase
                );
            }
        }
        else
        {
            // Fallback เฉพาะดาเมจพิเศษที่ไม่ใช่มอนสเตอร์ตีปกติ (เช่น ตกจากที่สูง)
            if (info.damageType == DamageType.FallDamage)
            {
                TriggerKick(defaultFOVKick * 1.5f, defaultDuration * 1.4f, defaultEase);
            }
        }
    }

    // ==================== Public API สำหรับผู้โจมตี (เมื่อฟันโดนศัตรู) ====================

    /// <summary>
    /// สั่งเอฟเฟกต์ FOV Kick สำหรับฝ่ายผู้โจมตี (เรียกเมื่อผู้เล่นฟันโดนเป้าหมาย)
    /// </summary>
    public void TriggerAttackerKick(ImpactProfile profile)
    {
        if (profile == null) return;

        if (profile.enableAttackerFOVKick)
        {
#if UNITY_EDITOR
            Debug.Log($"[CameraFOV] Attacker FOV Kick triggered via profile. Amount: {profile.attackerFOVKickAmount}, Duration: {profile.attackerFOVKickDuration}");
#endif
            TriggerKick(profile.attackerFOVKickAmount, profile.attackerFOVKickDuration, profile.attackerFOVKickEase);
        }
    }

    // ==================== Coroutine: FOV Kick ====================

    private IEnumerator FOVKickRoutine(float fovChange, float duration, EaseType ease)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float easedT = ApplyEasing(t, ease);

            // Kick = กระชากไปค่าเป้าหมายแล้วค่อยๆ กลับ
            // ที่ t=0 ค่าจะเปลี่ยนเต็ม, ที่ t=1 ค่าจะกลับเป็น 0
            kickFOVCurrent = fovChange * (1f - easedT);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // รีเซ็ต kick เมื่อเสร็จ
        kickFOVCurrent = 0f;
        fovCoroutine = null;
    }

    // ==================== Coroutine: FOV Smooth Transition ====================

    private IEnumerator FOVTransitionRoutine(float targetFOV, float duration, EaseType ease)
    {
        // ปิด kickFOVCurrent ชั่วคราวเพื่อให้ transition ทำงานกับ baseFOV โดยตรง
        kickFOVCurrent = 0f;

        float startFOV = baseFOV;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float easedT = ApplyEasing(t, ease);

            baseFOV = Mathf.Lerp(startFOV, targetFOV, easedT);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        baseFOV = targetFOV;
        fovCoroutine = null;
    }

    // ==================== Coroutine: FOV Pulse ====================

    private IEnumerator FOVPulseRoutine(float fovChange, float pulseDuration, int pulseCount)
    {
        for (int i = 0; i < pulseCount; i++)
        {
            float elapsed = 0f;
            float halfDuration = pulseDuration * 0.5f;

            // ครึ่งแรก: ขยาย FOV
            while (elapsed < halfDuration)
            {
                float t = elapsed / halfDuration;
                kickFOVCurrent = Mathf.Lerp(0f, fovChange, ApplyEasing(t, EaseType.EaseOut));

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;

            // ครึ่งหลัง: หด FOV กลับ
            while (elapsed < halfDuration)
            {
                float t = elapsed / halfDuration;
                kickFOVCurrent = Mathf.Lerp(fovChange, 0f, ApplyEasing(t, EaseType.EaseIn));

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        kickFOVCurrent = 0f;
        fovCoroutine = null;
    }

    // ==================== Easing Functions ====================

    /// <summary>
    /// คำนวณค่า Easing จาก t (0→1) ตาม EaseType ที่เลือก
    /// </summary>
    private float ApplyEasing(float t, EaseType ease)
    {
        switch (ease)
        {
            case EaseType.Linear:
                return t;

            case EaseType.EaseInOut:
                // SmoothStep: 3t² - 2t³
                return t * t * (3f - 2f * t);

            case EaseType.EaseIn:
                // Quadratic ease in: t²
                return t * t;

            case EaseType.EaseOut:
                // Quadratic ease out: 1 - (1-t)²
                return 1f - (1f - t) * (1f - t);

            case EaseType.Punch:
                // กระชากเร็วแล้วค่อยๆ กลับ (Exponential decay)
                // ยิ่ง t มาก ยิ่งใกล้ 1 (= ค่า kick → 0)
                return 1f - Mathf.Pow(1f - t, 3f);

            case EaseType.Elastic:
                // สปริงเด้ง (Damped sine wave)
                if (t >= 1f) return 1f;
                float p = 0.3f;
                return 1f - Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p);

            default:
                return t;
        }
    }
}
