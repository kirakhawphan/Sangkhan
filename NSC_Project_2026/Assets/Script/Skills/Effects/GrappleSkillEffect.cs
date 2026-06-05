using UnityEngine;
using Skills.Core;

namespace Skills.Effects
{
    [CreateAssetMenu(fileName = "New Grapple Effect", menuName = "Skills/Effects/Grapple")]
    public class GrappleSkillEffect : SkillEffect
    {
        [Header("Grapple Target Settings")]
        [Tooltip("ระยะค้นหาเป้าหมายสูงสุดเพื่อพุ่งใส่")]
        public float maxGrappleRange = 15f;
        [Tooltip("Layer ของศัตรูที่จับได้")]
        public LayerMask targetLayer;

        [Header("Dash Settings")]
        [Tooltip("กราฟความเร็วการพุ่ง แนะนำให้ขึ้นไวๆ แล้วชะลอตอนท้าย (Ease-Out)")]
        public AnimationCurve dashCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
        [Tooltip("เวลาที่ใช้พุ่งไปถึงตัวศัตรู (วินาที)")]
        public float dashDuration = 0.3f;
        [Tooltip("ระยะหยุดพุ่งก่อนถึงตัวศัตรู เพื่อไม่ให้โมเดลทะลุกัน")]
        public float stopDistance = 1.5f;

        [Header("Punch Damage")]
        public float damage = 100f;
        public float poiseDamage = 50f;
        public float knockbackPower = 15f;
        public DamageType damageType = DamageType.Combat;

        [Header("Impact (Game Feel)")]
        [Tooltip("โปรไฟล์กระแทก (สั่นหน้าจอ, FOV, Hit Stop)")]
        public ImpactProfile impactProfile;
        
        [Tooltip("ระยะเวลาหยุดเวลาชั่วคราวตอนยิงสลิงโดนก่อนพุ่ง (Anticipation)")]
        public float initialHitStopDuration = 0.05f;

        [Header("VFX")]
        [Tooltip("เอฟเฟกต์ที่จะเล่นตรงจุดที่ศัตรูโดนต่อย")]
        public GameObject hitVfxPrefab;

        public override void Execute(GameObject caster, SkillData skillData)
        {
            // 1. หาเป้าหมายที่ดีที่สุดด้านหน้า
            Transform casterTransform = caster.transform;
            
            // TargetDetector เป็น [System.Serializable] ไม่ใช่ MonoBehaviour
            // ต้องดึงผ่าน PlayerCombat ซึ่งเป็นคนถือ reference ของมันอยู่
            PlayerCombat playerCombat = caster.GetComponent<PlayerCombat>();
            TargetDetector detector = playerCombat != null ? playerCombat.GetTargetDetector() : null;
            
            Transform targetTransform = null;

            if (detector != null && detector.CurrentTarget != null)
            {
                // ตรวจสอบว่าเป้าหมายอยู่ในระยะที่ตั้งไว้หรือไม่
                float dist = Vector3.Distance(casterTransform.position, detector.CurrentTarget.transform.position);
                if (dist <= maxGrappleRange)
                {
                    targetTransform = detector.CurrentTarget.transform;
                }
            }

            // ถ้าหาจาก TargetDetector ไม่เจอ ลองหาด้วย SphereCast แทน
            if (targetTransform == null)
            {
                if (Physics.SphereCast(casterTransform.position, 1.5f, casterTransform.forward, out RaycastHit hit, maxGrappleRange, targetLayer))
                {
                    targetTransform = hit.collider.transform;
                }
            }

            // ถ้าไม่เจอศัตรูเลย ยกเลิกสกิล
            if (targetTransform == null)
            {
#if UNITY_EDITOR
                Debug.Log("[GrappleSkillEffect] ไม่มีเป้าหมายในระยะสำหรับยิงสลิง!");
#endif
                return;
            }

            // 2. เจอเป้าหมาย! สร้างจังหวะ Game Feel แรก (Anticipation Hit Stop)
            if (initialHitStopDuration > 0f && ImpactManager.Instance != null)
            {
                ImpactManager.Instance.HitStop(initialHitStopDuration);
            }

            // เล่นเสียงสลิงพุ่ง (ถ้ามีใน SkillData)
            if (skillData != null && skillData.sfxClip != null)
            {
                AudioSource.PlayClipAtPoint(skillData.sfxClip, casterTransform.position);
            }

            // 3. เริ่มการพุ่ง โดยแนบสคริปต์ GrappleController เข้าไปชั่วคราวเพื่อให้ขยับ CharacterController ข้ามเฟรมได้
            GrappleController grappleController = caster.GetComponent<GrappleController>();
            if (grappleController == null)
            {
                grappleController = caster.AddComponent<GrappleController>();
            }

            // ส่งข้อมูลไปให้ Controller จัดการต่อ
            grappleController.StartGrapple(caster, targetTransform, this, skillData);
            
#if UNITY_EDITOR
            Debug.Log($"[GrappleSkillEffect] เริ่มยิงสลิงพุ่งไปหา {targetTransform.name}!");
#endif
        }

        public override void DrawGizmos(Transform caster)
        {
            if (caster == null) return;

            Gizmos.color = gizmoColor;
            
            // วาดระยะหวังผลของ Grapple
            Vector3 endPos = caster.position + caster.forward * maxGrappleRange;
            Gizmos.DrawRay(caster.position, caster.forward * maxGrappleRange);
            Gizmos.DrawWireSphere(endPos, 1.5f); // ขนาด SphereCast คร่าวๆ
        }
    }
}
