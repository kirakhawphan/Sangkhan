using UnityEngine;

// ระบบจัดการ Hitbox ของอาวุธระยะประชิด (Zero GC Allocation)
public class MeleeHitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [SerializeField] private Transform attackPoint;      // จุดศูนย์กลางที่ใช้ตรวจจับการโจมตี (Hitbox Center)
    [SerializeField] private float attackRadius = 1.0f;  // รัศมีของ Hitbox
    [SerializeField] private LayerMask targetLayer;      // เลเยอร์ที่เราต้องการจะโจมตี

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 20f;   // ดาเมจพื้นฐาน
    [SerializeField] private float poiseDamage = 10f;    // ดาเมจทำลายเกราะ (ความถึก)
    [SerializeField] private float knockbackPower = 5f;  // ความแรงของการกระเด็น

    // Strict Rule: จอง Array ล่วงหน้า เพื่อป้องกัน GC Allocation ที่เกิดจากการสร้าง Array ใหม่ทุกครั้งที่โจมตี
    private readonly Collider[] hitResults = new Collider[10];

    // ฟังก์ชันนี้ถูกเรียกเมื่อเราทำการโจมตี (อาจเรียกผ่าน Animation Event, State Machine หรือ Input)
    public void PerformAttack()
    {
        if (attackPoint == null) return;

        // ใช้ OverlapSphereNonAlloc แทน OverlapSphere ธรรมดา
        // คำสั่งนี้จะนำผลลัพธ์ไปใส่ไว้ใน hitResults (ที่เราจองพื้นที่ไว้แล้ว) และคืนค่าจำนวนที่โดนจริงๆ กลับมา
        int hitCount = Physics.OverlapSphereNonAlloc(attackPoint.position, attackRadius, hitResults, targetLayer);

        // วนลูปตรวจสอบเฉพาะ object ที่ถูกโจมตีจริงๆ (ตามจำนวน hitCount)
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitResults[i];

            // ค้นหา IDamageable ในตัว Object นั้นๆ รวมถึงใน Parent ด้วย
            // (ช่วยให้ไม่ต้องผูกมัดว่า Collider กับ Script ต้องอยู่ก้อนเดียวกันเสมอไป)
            IDamageable damageable = col.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                // [แก้] คำนวณทิศทางการกระเด็น และบังคับให้อยู่ในแนวนอน (y = 0) ป้องกันแรงกดลงพื้น
                Vector3 knockbackDir = col.transform.position - transform.position;
                knockbackDir.y = 0;
                knockbackDir = knockbackDir.normalized;
                
                // สร้าง DamageInfo ในรูปแบบของ Struct (ไม่มีการ Alloc Memory บน Heap = Zero GC)
                DamageInfo info = new DamageInfo
                {
                    damageAmount = attackDamage,
                    poiseDamage = poiseDamage,
                    hitPoint = col.ClosestPoint(attackPoint.position), // หาจุดที่ใกล้ที่สุดบน Collider เพื่อเล่นเอฟเฟกต์
                    knockbackForce = knockbackDir * knockbackPower,
                    attacker = this.gameObject
                };

                // ส่งคำสั่ง TakeDamage ให้กับเป้าหมายที่ถูกตี
                damageable.TakeDamage(info);
            }
        }
    }

    // ฟังก์ชันช่วยเหลือสำหรับแสดงเส้นวงกลม Hitbox ในหน้า Scene เพื่อให้กะระยะได้ง่ายขึ้น
    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}
