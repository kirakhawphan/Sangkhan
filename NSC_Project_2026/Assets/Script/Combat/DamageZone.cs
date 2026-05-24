using UnityEngine;

// พื้นที่อันตราย: เมื่อตัวละครเหยียบจะได้รับดาเมจต่อเนื่อง (Damage Over Time)
// ใช้ร่วมกับระบบ IDamageable / HealthSystem ที่มีอยู่แล้ว
public class DamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("ดาเมจต่อครั้ง")]
    [SerializeField] private float damagePerTick = 10f;

    [Tooltip("ระยะเวลาระหว่างแต่ละครั้งที่โดนดาเมจ (วินาที)")]
    [SerializeField] private float tickInterval = 0.5f;

    [Tooltip("ประเภทดาเมจ (Combat = สั่นกล้อง, System = ไม่สั่น, Poison = พิษ)")]
    [SerializeField] private DamageType damageType = DamageType.System;

    // คลาสสำหรับเก็บเป้าหมายและเวลาแยกกันแต่ละตัวละคร (Zero GC/Optimized)
    private class TargetInZone
    {
        public IDamageable Target;
        public float Timer;
        public int ColliderCount;
        public Collider RepresentativeCollider;
    }

    private readonly System.Collections.Generic.List<TargetInZone> targetsInZone = new System.Collections.Generic.List<TargetInZone>();

    private void Update()
    {
        float dt = Time.deltaTime;

        // วนลูปนับเวลาถอยหลังแต่ละเป้าหมายแยกกัน (วนถอยหลังเพื่อความปลอดภัยในการลบข้อมูลหากเป้าหมายถูกทำลาย)
        for (int i = targetsInZone.Count - 1; i >= 0; i--)
        {
            TargetInZone entry = targetsInZone[i];

            // ป้องกันกรณีเป้าหมายถูกลบหรือทำลายกลางอากาศ (Safety check)
            if (entry.Target == null || (entry.Target as Component) == null)
            {
                targetsInZone.RemoveAt(i);
                continue;
            }

            entry.Timer -= dt;

            if (entry.Timer <= 0f)
            {
                TryDealDamage(entry.Target, entry.RepresentativeCollider);
                entry.Timer = tickInterval; // รีเซ็ตเวลาคูลดาวน์ดาเมจเฉพาะตัว
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        // ค้นหาเป้าหมายเดิมใน List
        TargetInZone existingEntry = null;
        for (int i = 0; i < targetsInZone.Count; i++)
        {
            if (targetsInZone[i].Target == target)
            {
                existingEntry = targetsInZone[i];
                break;
            }
        }

        if (existingEntry != null)
        {
            // หากเป้าหมายมี Collider อื่นเข้ามาก่อนแล้ว ให้เพิ่มจำนวนการนับ Collider เฉยๆ (ป้องกันดาเมจเบิ้ล)
            existingEntry.ColliderCount++;
        }
        else
        {
            // โดนทันทีเมื่อก้าวเหยียบเข้ามาในโซนรอบแรก
            TryDealDamage(target, other);

            // บันทึกเป้าหมายใหม่ลง List
            TargetInZone newEntry = new TargetInZone
            {
                Target = target,
                Timer = tickInterval,
                ColliderCount = 1,
                RepresentativeCollider = other
            };
            targetsInZone.Add(newEntry);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        // ค้นหาเป้าหมายใน List
        TargetInZone existingEntry = null;
        int index = -1;
        for (int i = 0; i < targetsInZone.Count; i++)
        {
            if (targetsInZone[i].Target == target)
            {
                existingEntry = targetsInZone[i];
                index = i;
                break;
            }
        }

        if (existingEntry != null)
        {
            existingEntry.ColliderCount--;

            // หากไม่เหลือ Collider ใดๆ ของตัวนี้ในโซนแล้ว ให้ลบออกจาก List
            if (existingEntry.ColliderCount <= 0)
            {
                targetsInZone.RemoveAt(index);
            }
            else if (existingEntry.RepresentativeCollider == other)
            {
                // หาก Collider ที่ใช้เป็นตัวแทนออกไป แต่ยังมี Collider อื่นอยู่ ให้ใช้ตัวอื่นทดแทน (Safety fallback)
                existingEntry.RepresentativeCollider = (target as Component).GetComponentInChildren<Collider>();
            }
        }
    }

    /// <summary>
    /// พยายามส่งดาเมจไปยังเป้าหมายที่อยู่ในโซน
    /// </summary>
    private void TryDealDamage(IDamageable target, Collider other)
    {
        if (target == null) return;

        DamageInfo info = new DamageInfo
        {
            damageAmount = damagePerTick,
            damageType = this.damageType, // ใช้ค่าจาก Inspector
            hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position,
            knockbackForce = Vector3.zero, // พื้นไม่มีแรงกระเด็น
            attacker = gameObject
        };

        target.TakeDamage(info);
    }
}
