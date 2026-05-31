using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่เดียวกับ PossessionManager หรือ GameManager ในฉาก
public class PGController : MonoBehaviour
{
    [Header("PG Settings")]
    [SerializeField, Tooltip("ค่า PG สูงสุดเมื่อเริ่มสิงร่าง")] 
    private float maxPG = 100f;
    
    [SerializeField, Tooltip("PG ลดลงวินาทีละเท่าไหร่")] 
    private float pgDecreaseRate = 5f;

    [Header("Penalty Settings")]
    [SerializeField, Tooltip("เมื่อ PG หมด เลือดผู้เล่นจะลดลงวินาทีละเท่าไหร่")] 
    private float healthPenaltyRate = 10f;

    [Header("Debug")]
    [SerializeField] private float currentPG;
    [SerializeField] private bool isActive = false;

    // เผื่อไว้ให้ UI นำไปใช้แสดงหลอด PG
    public event System.Action<float> OnPGChanged;
    public float CurrentPGPercentage => maxPG > 0 ? currentPG / maxPG : 0;

    private void Start()
    {
        currentPG = maxPG;
        
        if (PossessionManager.Instance != null)
        {
            // ดักจับอีเวนต์เมื่อมีการสิงร่างใหม่ ให้รีเซ็ต PG
            PossessionManager.Instance.OnPossessionChanged += ResetPG;
            
            // ถ้าเริ่มเกมมาแล้วมีร่างสิงอยู่ ให้ระบบทำงานทันที
            if (PossessionManager.Instance.CurrentBody != null)
            {
                isActive = true;
            }
        }
    }

    private void OnDestroy()
    {
        if (PossessionManager.Instance != null)
        {
            PossessionManager.Instance.OnPossessionChanged -= ResetPG;
        }
    }

    /// <summary>
    /// รีเซ็ตค่า PG ให้เต็ม และเริ่มลดค่า
    /// </summary>
    public void ResetPG()
    {
        currentPG = maxPG;
        isActive = true;
        OnPGChanged?.Invoke(CurrentPGPercentage);
    }

    private void Update()
    {
        // ถ้าระบบยังไม่ทำงาน (เช่นยังไม่ได้สิงใคร) ให้ออกไปก่อน
        if (!isActive) return;

        if (currentPG > 0)
        {
            // ลดค่า PG ลงตามเวลา (วินาที)
            currentPG -= pgDecreaseRate * Time.deltaTime;
            currentPG = Mathf.Max(currentPG, 0); // ไม่ให้ค่าติดลบ
            
            // อัปเดต UI ถ้ามีการสมัครรับ Event เอาไว้
            OnPGChanged?.Invoke(CurrentPGPercentage);
        }
        else
        {
            // PG หมด (<= 0) เริ่มหักเลือดของผู้เล่นแทน
            ApplyHealthPenalty();
        }
    }

    private void ApplyHealthPenalty()
    {
        if (PossessionManager.Instance == null || PossessionManager.Instance.CurrentBody == null) 
            return;

        // หา HealthSystem ของร่างปัจจุบันที่ผู้เล่นสิงอยู่
        HealthSystem currentHealth = PossessionManager.Instance.CurrentBody.GetComponent<HealthSystem>();
        
        if (currentHealth != null)
        {
            // สร้างข้อมูลดาเมจ
            DamageInfo penaltyDamage = new DamageInfo
            {
                damageAmount = healthPenaltyRate * Time.deltaTime, // ลดตามเวลาแบบ DOT (Damage Over Time)
                poiseDamage = 0,                                   // ไม่ทำลายเกราะการชะงัก
                damageType = DamageType.Possession,                // ระบุว่าเป็นประเภท ดาเมจจากการสิงร่าง
                knockbackForce = Vector3.zero,
                attacker = gameObject
            };
            
            // ลดเลือดเป้าหมาย
            currentHealth.TakeDamage(penaltyDamage);
        }
    }
}
