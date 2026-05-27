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
    [SerializeField] private DamageType damageType = DamageType.Combat; // ประเภทของดาเมจ (ให้เลือกได้ว่าเป็นผู้เล่นตี หรือ ศัตรูตี)
    [SerializeField] private float knockbackPower = 5f;  // ความแรงของการกระเด็น

    [Header("Game Feel Settings (Hit Impact)")]
    [SerializeField] private ImpactProfile impactProfile; // [แก้] โปรไฟล์ความแรงของการกระทบ (กล้องสั่น, FOV kick, Hit Stop)

    // Strict Rule: จอง Array ล่วงหน้า เพื่อป้องกัน GC Allocation ที่เกิดจากการสร้าง Array ใหม่ทุกครั้งที่โจมตี
    private readonly Collider[] hitResults = new Collider[10];

    // แคชเก็บเป้าหมายที่โดนฟันแล้วในเฟรมนี้ เพื่อป้องกันการยิงดาเมจซ้ำกับเป้าหมายเดิม (Zero GC)
    private readonly System.Collections.Generic.List<IDamageable> damagedTargets = new System.Collections.Generic.List<IDamageable>(10);

    // แคชเอาไว้ล่วงหน้าเพื่อหลีกเลี่ยงการดึงใหม่ทุกครั้งใน Loop (Zero GC)
    private IDamageable myDamageable;

    private void Awake()
    {
        myDamageable = GetComponentInParent<IDamageable>();
    }

    // ฟังก์ชันนี้ถูกเรียกเมื่อเราทำการโจมตี (อาจเรียกผ่าน Animation Event, State Machine หรือ Input)
    public bool PerformAttack()
    {
        if (attackPoint == null) return false;

        // ล้างข้อมูลเป้าหมายที่เคยฟันในรอบนี้ก่อนเริ่ม (Zero GC)
        damagedTargets.Clear();

        // ใช้ OverlapSphereNonAlloc แทน OverlapSphere ธรรมดา
        // คำสั่งนี้จะนำผลลัพธ์ไปใส่ไว้ใน hitResults (ที่เราจองพื้นที่ไว้แล้ว) และคืนค่าจำนวนที่โดนจริงๆ กลับมา
        int hitCount = Physics.OverlapSphereNonAlloc(attackPoint.position, attackRadius, hitResults, targetLayer);
#if UNITY_EDITOR
        Debug.Log($"[MeleeHitbox] '{gameObject.name}' ตรวจพบ Collider ในระยะ {hitCount} ชิ้น (ค้นหาใน Layer: {targetLayer.value})");
#endif

        bool hasHitTarget = false;

        // วนลูปตรวจสอบเฉพาะ object ที่ถูกโจมตีจริงๆ (ตามจำนวน hitCount)
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitResults[i];

            // ค้นหา IDamageable ในตัว Object นั้นๆ รวมถึงใน Parent ด้วย
            IDamageable targetDamageable = col.GetComponentInParent<IDamageable>();
            // myDamageable ถูกเรียกใช้งานจากของที่ Cache ไว้แล้ว (ไม่ต้อง GetComponentInParent ซ้ำซ้อน)

#if UNITY_EDITOR
            Debug.Log($"   -> ตรวจสอบ: '{col.gameObject.name}' | เจอเป้าหมาย: {(targetDamageable != null)} | ตีตัวเองไหม?: {(targetDamageable == myDamageable)}");
#endif

            // เช็คว่าเจอเป้าหมาย และเป้าหมายนั้นต้อง 'ไม่ใช่' ตัวเราเอง และยังไม่เคยถูกโจมตีในการฟันกวาดรอบนี้
            if (targetDamageable != null && targetDamageable != myDamageable && !damagedTargets.Contains(targetDamageable))
            {
                // เพิ่มเป้าหมายนี้เข้าไปใน List ป้องกันการโดนซ้ำในเฟรมเดียวกัน
                damagedTargets.Add(targetDamageable);
                hasHitTarget = true;
#if UNITY_EDITOR
                Debug.Log($"   => ฟันเข้าเป้า! ส่งดาเมจไปที่ '{col.gameObject.name}'");
#endif

                // หากคนตีคือตัวละครที่ผู้เล่นควบคุมอยู่ ให้เช็คเรื่องโปรไฟล์ครอบทับของผู้เล่นหลัก (Option B)
                Playermovement pm = GetComponentInParent<Playermovement>();
                bool isPlayerAttacking = (pm != null && pm.isPossessed);

                ImpactProfile activeProfile = this.impactProfile;
                if (isPlayerAttacking && PossessionManager.Instance != null && PossessionManager.Instance.PlayerGlobalImpactProfile != null)
                {
                    activeProfile = PossessionManager.Instance.PlayerGlobalImpactProfile;
                }

                // [Game Feel] สั่งหยุดเวลา (Hit Stop) ตามค่าที่ตั้งไว้ใน Profile ที่ใช้งาน
                float currentHitStop = activeProfile != null ? activeProfile.hitStopDuration : 0.08f;
                if (ImpactManager.Instance != null && currentHitStop > 0f)
                {
                    ImpactManager.Instance.HitStop(currentHitStop);
                }

                // หาจุดกึ่งกลางของคนตี (ถ้ามี HealthSystem ให้ยึดจากตรงนั้น ไม่งั้นใช้ Root)
                Transform attackerBody = (myDamageable as Component)?.transform ?? transform.root;
                Transform targetBody = (targetDamageable as Component)?.transform ?? col.transform;

                // [แก้] คำนวณทิศทางการกระเด็นจากตัวคนตี -> คนถูกตี เพื่อป้องกันแรงกระเด็นเพี้ยนเวลาเหวี่ยงแขน
                Vector3 knockbackDir = targetBody.position - attackerBody.position;
                knockbackDir.y = 0;
                if (knockbackDir.sqrMagnitude < 0.001f) knockbackDir = attackerBody.forward; // กันบั๊กทับกันพอดี
                knockbackDir = knockbackDir.normalized;
                
                // สร้าง DamageInfo ในรูปแบบของ Struct (ไม่มีการ Alloc Memory บน Heap = Zero GC)
                DamageInfo info = new DamageInfo
                {
                    damageAmount = attackDamage,
                    poiseDamage = poiseDamage,
                    damageType = this.damageType, // ส่งประเภทดาเมจที่ตั้งไว้ไปให้เป้าหมาย
                    hitPoint = col.ClosestPoint(attackPoint.position), // หาจุดที่ใกล้ที่สุดบน Collider เพื่อเล่นเอฟเฟกต์
                    knockbackForce = knockbackDir * knockbackPower,
                    attacker = this.gameObject,
                    impactProfile = activeProfile // [เพิ่ม] แนบ Profile (รวมถึงโปรไฟล์ผู้เล่นครอบทับ) ไปกับดาเมจด้วย
                };

                // ส่งคำสั่ง TakeDamage ให้กับเป้าหมายที่ถูกตี
                targetDamageable.TakeDamage(info);

                // หากคนตีคือตัวละครที่ผู้เล่นควบคุมอยู่ ให้ส่งสัญญาณการสั่นกล้อง/FOV ขาตี (Attacker Feedback)
#if UNITY_EDITOR
                Debug.Log($"[MeleeHitbox] Check Camera Trigger -> Found Playermovement: {pm != null}, isPossessed: {pm?.isPossessed}, Has ActiveProfile: {activeProfile != null}");
#endif
                if (isPlayerAttacking && activeProfile != null)
                {
                    if (CameraShake.Instance != null) CameraShake.Instance.TriggerAttackerShake(activeProfile);
                    if (CameraFOV.Instance != null) CameraFOV.Instance.TriggerAttackerKick(activeProfile);
                }
            }
        }

        return hasHitTarget;
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

    // [เพิ่ม] อนุญาตให้ระบบ Possession เปลี่ยนเป้าหมายการโจมตีได้
    public void SetTargetLayer(LayerMask newLayer)
    {
        targetLayer = newLayer;
    }

    // [เพิ่ม] อนุญาตให้ระบบ Possession เปลี่ยนประเภทของดาเมจได้
    public void SetDamageType(DamageType newType)
    {
        damageType = newType;
    }
}
