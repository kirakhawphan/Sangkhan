using UnityEngine;
using Skills.Core;

namespace Skills.Effects
{
    /// <summary>
    /// สคริปต์นี้ถูกแนบ (Add Component) ชั่วคราวไปที่ตัว Player เมื่อใช้ GrappleSkillEffect
    /// ทำหน้าที่คำนวณการพุ่งเข้าชนศัตรูแบบเฟรมต่อเฟรม (Over-time Dash)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GrappleController : MonoBehaviour
    {
        private CharacterController charController;
        private Playermovement playerMovement;
        
        private Transform targetTransform;
        private GrappleSkillEffect grappleData;
        private SkillData skillData;
        
        private Vector3 startPosition;
        private Vector3 targetPosition;
        
        private float currentDashTime;
        private bool isGrappling = false;

        private void Awake()
        {
            charController = GetComponent<CharacterController>();
            playerMovement = GetComponent<Playermovement>();
        }

        public void StartGrapple(GameObject caster, Transform target, GrappleSkillEffect effectData, SkillData sData)
        {
            targetTransform = target;
            grappleData = effectData;
            skillData = sData;
            
            startPosition = transform.position;
            
            // หาจุดหมายปลายทาง โดยหักลบระยะ stopDistance ออกไปเพื่อไม่ให้ชนเป้าหมายทะลุไปเลย
            Vector3 directionToTarget = (target.position - startPosition).normalized;
            targetPosition = target.position - (directionToTarget * grappleData.stopDistance);
            // บังคับแกน Y ให้อยู่ระดับเดียวกัน (ถ้ายากให้ลอยขึ้นไปตีกลางอากาศ ค่อยปรับปรุงตรงนี้)
            targetPosition.y = startPosition.y;

            currentDashTime = 0f;
            isGrappling = true;

            // บังคับให้หน้าหันไปหาเป้าหมาย
            directionToTarget.y = 0f;
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(directionToTarget);
            }

            // ถ้ามี Playermovement ให้ทำการขยาย FOV เพื่อสร้างความรู้สึกว่าพุ่งเร็ว (Game Feel)
            if (playerMovement != null && playerMovement.isPossessed && CameraFOV.Instance != null)
            {
                CameraFOV.Instance.SetSprinting(true); // ยืมใช้ระบบวิ่งเพื่อเพิ่ม FOV แบบง่ายๆ
            }
        }

        private void Update()
        {
            if (!isGrappling) return;

            currentDashTime += Time.deltaTime;

            if (currentDashTime >= grappleData.dashDuration)
            {
                // ถึงเป้าหมายแล้ว
                FinishGrapple();
                return;
            }

            // คำนวณความเร็วในการพุ่งผ่าน Curve
            float normalizedTime = currentDashTime / grappleData.dashDuration;
            
            // เราอยากได้ระยะทางที่ต้องขยับในเฟรมนี้ 
            // วิธีหนึ่งคือการ Lerp ตำแหน่งตาม Curve ตรงๆ แล้วให้ CharacterController วิ่งไปจุดนั้น
            float curveValue = grappleData.dashCurve.Evaluate(normalizedTime);
            Vector3 expectedPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            // ขยับ CharacterController ไปยัง expectedPosition
            Vector3 moveDelta = expectedPosition - transform.position;
            
            // ใช้ Move() แทนการเซ็ต transform.position เพื่อไม่ให้ทะลุกำแพงถ้าเป้าหมายอยู่หลังกำแพง
            charController.Move(moveDelta);
        }

        private void FinishGrapple()
        {
            isGrappling = false;

            // ปิด FOV แบบพุ่ง
            if (playerMovement != null && playerMovement.isPossessed && CameraFOV.Instance != null)
            {
                CameraFOV.Instance.SetSprinting(false);
            }

            ExecutePunch();

            // ลบสคริปต์ตัวเองทิ้งหลังใช้งานเสร็จเพื่อไม่ให้รก
            Destroy(this);
        }

        private void ExecutePunch()
        {
            if (targetTransform == null) return;

            // 1. ตรวจสอบเป้าหมายว่าเป็น IDamageable หรือไม่
            IDamageable damageable = targetTransform.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Vector3 directionToTarget = (targetTransform.position - transform.position).normalized;
                directionToTarget.y = 0f;

                // 2. ส่งค่าความเสียหาย (DamageInfo)
                DamageInfo info = new DamageInfo
                {
                    damageAmount = grappleData.damage,
                    poiseDamage = grappleData.poiseDamage,
                    damageType = grappleData.damageType,
                    hitPoint = targetTransform.position + Vector3.up * 1f, // จุดที่โดนตีสมมติว่ากลางตัว
                    knockbackForce = directionToTarget * grappleData.knockbackPower,
                    attacker = gameObject,
                    impactProfile = grappleData.impactProfile
                };

                damageable.TakeDamage(info);

                // 3. จอสั่นหนักๆ เมื่อต่อยโดน (Game Feel)
                if (playerMovement != null && playerMovement.isPossessed && grappleData.impactProfile != null)
                {
                    if (CameraShake.Instance != null)
                        CameraShake.Instance.TriggerAttackerShake(grappleData.impactProfile);
                    
                    if (CameraFOV.Instance != null)
                        CameraFOV.Instance.TriggerAttackerKick(grappleData.impactProfile);
                }

                // 4. Hit Stop กระตุกเวลาตอนต่อยโดนแบบเน้นๆ
                if (grappleData.impactProfile != null && grappleData.impactProfile.hitStopDuration > 0f)
                {
                    if (ImpactManager.Instance != null)
                        ImpactManager.Instance.HitStop(grappleData.impactProfile.hitStopDuration);
                }
            }

            // 5. แสดง VFX ต่อย (ถ้ามี)
            if (grappleData.hitVfxPrefab != null)
            {
                Instantiate(grappleData.hitVfxPrefab, targetTransform.position + Vector3.up * 1f, transform.rotation);
            }
        }
    }
}
