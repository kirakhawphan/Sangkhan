using UnityEngine;

// ระบบเลือดที่เป็นอิสระ (Decoupled) ไม่ผูกกับคลาส Player หรือ Enemy โดยตรง
public class HealthSystem : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // อีเวนต์สำหรับส่งค่า % เลือด (0.0 ถึง 1.0) ไปให้ UI หรือระบบอื่นที่ติดตามอยู่
    public event System.Action<float> OnHealthChanged;
    // อีเวนต์สำหรับแจ้งเตือนเมื่อเป้าหมายตาย
    public event System.Action OnDeath;

    private bool isDead = false;

    private void Awake()
    {
        // กำหนดเลือดให้เต็มเมื่อเริ่มต้น
        currentHealth = maxHealth;
    }

    private void Start()
    {
        // ส่งอีเวนต์ค่าเลือดเริ่มต้น เพื่อให้ UI อัปเดตเมื่อเริ่มเกม
        OnHealthChanged?.Invoke(GetHealthPercentage());
    }

    // ฟังก์ชันรับดาเมจตามข้อบังคับของ IDamageable
    public void TakeDamage(DamageInfo info)
    {
        if (isDead) return;

        // ลดเลือดตามจำนวนดาเมจ
        currentHealth -= info.damageAmount;
        
        // ป้องกันไม่ให้เลือดติดลบ (Clamp ไว้ที่ 0)
        currentHealth = Mathf.Max(currentHealth, 0f);

        // ยิงอีเวนต์แจ้งเตือนว่าเลือดเปลี่ยนไปเท่าไหร่แล้ว
        OnHealthChanged?.Invoke(GetHealthPercentage());

        // ตรวจสอบการตาย
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        
        // ยิงอีเวนต์เมื่อตาย เพื่อให้สคริปต์อื่นมาเกาะ (เช่น เล่นแอนิเมชันตาย, ดรอปของ)
        OnDeath?.Invoke();
    }

    // ฟังก์ชันช่วยเหลือสำหรับคำนวณ % เลือด เพื่อความสะดวก
    private float GetHealthPercentage()
    {
        if (maxHealth <= 0) return 0f;
        return currentHealth / maxHealth;
    }
}
