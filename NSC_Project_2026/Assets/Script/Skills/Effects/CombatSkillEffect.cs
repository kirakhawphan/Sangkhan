using UnityEngine;
using Skills.Core;

namespace Skills.Effects
{
    /// <summary>
    /// สกิลต่อสู้: ปล่อยคลื่นพลังด้านหน้าตัวละคร (Shockwave)
    /// ทำดาเมจสูง + Knockback หนัก ในรูปทรงพัด (Fan Shape)
    /// </summary>
    [CreateAssetMenu(fileName = "New Combat Effect", menuName = "Skills/Effects/Combat")]
    public class CombatSkillEffect : SkillEffect
    {
        [Header("Shockwave Settings")]
        [Tooltip("ระยะของคลื่นกระแทก (จากตัวละคร)")]
        public float range = 4f;

        [Tooltip("มุมกว้างของพัด (องศา) — 360 = รอบตัว, 180 = ครึ่งวงกลมด้านหน้า, 90 = เฉพาะข้างหน้า")]
        [Range(30f, 360f)]
        public float fanAngle = 180f;

        [Header("Damage")]
        public float damage = 80f;
        public float poiseDamage = 25f;
        public float knockbackPower = 12f;
        public DamageType damageType = DamageType.Combat;
        public LayerMask targetLayer;

        [Header("Impact")]
        [Tooltip("โปรไฟล์กระแทก")]
        public ImpactProfile impactProfile;

        [Header("Shockwave Offset")]
        [Tooltip("เลื่อนจุดศูนย์กลางไปข้างหน้า (เมตร)")]
        public float forwardOffset = 1f;

        // จอง Array ล่วงหน้า (Zero GC)
        private readonly Collider[] hitResults = new Collider[20];

        public override void Execute(GameObject caster, SkillData skillData)
        {
            Transform casterTransform = caster.transform;
            Vector3 center = casterTransform.position + casterTransform.forward * forwardOffset;

            // --- 1. ตรวจจับเป้าหมาย ---
            int hitCount = Physics.OverlapSphereNonAlloc(center, range, hitResults, targetLayer);
            
#if UNITY_EDITOR
            Debug.Log($"[CombatSkillEffect] เริ่มทำงาน! ค้นหาเป้าหมายในระยะ {range} เมตร (Layer: {targetLayer.value}) พบ Collider ทั้งหมด {hitCount} ชิ้น");
#endif

            // แคช IDamageable ของตัวเอง (ป้องกันตีตัวเอง)
            IDamageable selfDamageable = caster.GetComponentInParent<IDamageable>();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitResults[i];
                if (col == null) continue;
                
                IDamageable target = col.GetComponentInParent<IDamageable>();

                if (target == null)
                {
#if UNITY_EDITOR
                    Debug.Log($"[CombatSkillEffect] ปฏิเสธ {col.name} เพราะไม่มีสคริปต์ IDamageable");
#endif
                    continue;
                }
                if (target == selfDamageable)
                {
#if UNITY_EDITOR
                    Debug.Log($"[CombatSkillEffect] ปฏิเสธ {col.name} เพราะเป็นผู้ร่ายสกิล (ตีตัวเอง)");
#endif
                    continue;
                }

                // --- 2. เช็คมุมพัด (Fan Angle) ---
                Vector3 dirToTarget = col.transform.position - casterTransform.position;
                dirToTarget.y = 0f;

                // ถ้ามุมระหว่างหน้าตัวละครกับเป้าหมาย > ครึ่งหนึ่งของมุมพัด → ข้ามไป
                if (fanAngle < 360f)
                {
                    float angle = Vector3.Angle(casterTransform.forward, dirToTarget);
                    if (angle > fanAngle * 0.5f)
                    {
#if UNITY_EDITOR
                        Debug.Log($"[CombatSkillEffect] ปฏิเสธ {col.name} เพราะอยู่นอกมุมที่ตั้งไว้ (มุมปัจจุบัน: {angle} > {fanAngle * 0.5f})");
#endif
                        continue;
                    }
                }

                // --- 3. คำนวณ Knockback ---
                Vector3 knockbackDir = dirToTarget.normalized;
                if (knockbackDir.sqrMagnitude < 0.001f) knockbackDir = casterTransform.forward;

                // --- 4. สร้าง DamageInfo ---
                DamageInfo info = new DamageInfo
                {
                    damageAmount = damage,
                    poiseDamage = poiseDamage,
                    damageType = damageType,
                    hitPoint = col.ClosestPoint(center),
                    knockbackForce = knockbackDir * knockbackPower,
                    attacker = caster,
                    impactProfile = impactProfile
                };

#if UNITY_EDITOR
                Debug.Log($"[CombatSkillEffect] กำลังทำดาเมจใส่ {(target as MonoBehaviour)?.gameObject.name} จำนวน {damage} ดาเมจ!");
#endif
                bool poiseBroken = target.TakeDamage(info);

                // --- 5. Hit Stop ---
                if (impactProfile != null && impactProfile.hitStopDuration > 0f)
                {
                    if (ImpactManager.Instance != null)
                        ImpactManager.Instance.HitStop(impactProfile.hitStopDuration);
                }

                // เช็คว่าคนตีเป็นผู้เล่น → สั่นกล้อง
                Playermovement pm = caster.GetComponent<Playermovement>();
                if (pm != null && pm.isPossessed && impactProfile != null)
                {
                    if (CameraShake.Instance != null)
                        CameraShake.Instance.TriggerAttackerShake(impactProfile);
                    if (CameraFOV.Instance != null)
                        CameraFOV.Instance.TriggerAttackerKick(impactProfile);
                }
            }

            // --- 6. VFX ---
            if (skillData != null && skillData.vfxPrefab != null)
            {
                Instantiate(skillData.vfxPrefab, center, casterTransform.rotation);
            }
        }

        public override void DrawGizmos(Transform caster)
        {
            if (caster == null) return;

            Vector3 center = caster.position + caster.forward * forwardOffset;
            
            // ใช้สีที่ตั้งค่ามาจาก Base Class (ปรับใน Inspector ได้เลย)
            Gizmos.color = gizmoColor;

            // ถ้าเป็น 360 องศา วาดวงกลมปกติ
            if (fanAngle >= 360f)
            {
                Gizmos.DrawWireSphere(center, range);
            }
            else
            {
                // วาดรูปร่างพัดคร่าวๆ
                Vector3 leftDir = Quaternion.Euler(0, -fanAngle * 0.5f, 0) * caster.forward;
                Vector3 rightDir = Quaternion.Euler(0, fanAngle * 0.5f, 0) * caster.forward;

                Gizmos.DrawRay(center, leftDir * range);
                Gizmos.DrawRay(center, rightDir * range);
                Gizmos.DrawRay(center, caster.forward * range);
                
                // ใช้ WireSphere ช่วยให้เห็นความโค้ง
                Gizmos.DrawWireSphere(center, range);
            }
        }
    }
}
