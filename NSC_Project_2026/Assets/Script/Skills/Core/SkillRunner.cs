using UnityEngine;

namespace Skills.Core
{
    /// <summary>
    /// ติดไว้ที่ทุกร่างที่มีสกิล (ทั้ง Player และ Enemy ที่สิงได้)
    /// รับคำสั่งจาก PlayerCombat หรือ AI แล้วเรียก SkillData.effect.Execute()
    /// 
    /// [ระบบดาเมจตรงจังหวะ]
    /// ดาเมจจะไม่เกิดทันทีที่กดปุ่ม แต่จะรอจนกว่า Animation Event จะเรียก AE_TriggerSkillEffect()
    /// ณ เฟรมที่อาวุธฟาดลงพอดี ทำให้ดาเมจตรงกับภาพ 100%
    /// </summary>
    public class SkillRunner : MonoBehaviour
    {
        [Header("Skill Slots (ลากสกิลมาใส่ได้เลย)")]
        [SerializeField] private SkillSlot[] skillSlots = new SkillSlot[2]; // 2 สล็อตเริ่มต้น (Q, R)

        private Animator cachedAnimator;

        // --- Pending Skill (สกิลที่รอ Animation Event เรียก) ---
        private SkillData pendingSkillData;
        private bool hasPendingSkill;

        // Zero GC: Cache Animator Hash
        private static readonly int HashSkillIndex = Animator.StringToHash("SkillIndex");

        private void Awake()
        {
            cachedAnimator = GetComponentInChildren<Animator>();

            // ซิงค์ค่า Cooldown จาก SkillData ทุกสล็อต
            for (int i = 0; i < skillSlots.Length; i++)
            {
                skillSlots[i]?.SyncCooldown();
            }
        }

        private void Update()
        {
            // เช็คปุ่มกดของแต่ละสล็อตสกิลอัตโนมัติ
            if (skillSlots == null) return;
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] != null && skillSlots[i].skillData != null)
                {
                    if (Input.GetKeyDown(skillSlots[i].activationKey))
                    {
                        TryUseSkill(i);
                    }
                }
            }
        }

        /// <summary>
        /// ใช้สกิลในสล็อตที่ระบุ (เรียกจาก PlayerCombat หรือ Input)
        /// ดาเมจจะยังไม่เกิดทันที — รอ Animation Event เรียก AE_TriggerSkillEffect() ก่อน
        /// </summary>
        /// <param name="slotIndex">ลำดับสล็อต (0 = Q, 1 = R)</param>
        /// <returns>true หากใช้สกิลสำเร็จ</returns>
        public bool TryUseSkill(int slotIndex)
        {
            // Guard Clause: ตรวจสอบความถูกต้อง
            if (skillSlots == null || slotIndex < 0 || slotIndex >= skillSlots.Length) return false;

            SkillSlot slot = skillSlots[slotIndex];
            if (slot == null || slot.skillData == null) return false;
            if (!slot.cooldown.IsReady()) return false;

            // เริ่มคูลดาวน์
            slot.cooldown.StartCooldown();

            // เล่นแอนิเมชัน (ถ้ามี Trigger ตั้งไว้)
            if (cachedAnimator != null && !string.IsNullOrEmpty(slot.skillData.animationTrigger))
            {
                cachedAnimator.SetTrigger(slot.skillData.animationTrigger);
            }

            // [แก้ไข] เก็บสกิลไว้รอ Animation Event เรียก — ไม่ยิงดาเมจทันที
            pendingSkillData = slot.skillData;
            hasPendingSkill = true;

            // [เพิ่ม] ล็อกการเดินของผู้เล่นตามระยะเวลา movementLockTime ใน SkillData
            PlayerCombat pc = GetComponentInParent<PlayerCombat>();
            if (pc != null)
            {
                pc.LockMovementForSkill(slot.skillData.movementLockTime);
            }

#if UNITY_EDITOR
            Debug.Log($"[SkillRunner] เตรียมสกิล '{slot.skillData.skillName}' สำเร็จ — รอ Animation Event เรียกดาเมจ...");
#endif

            // เล่น SFX (ถ้ามี)
            if (slot.skillData.sfxClip != null)
            {
                AudioSource.PlayClipAtPoint(slot.skillData.sfxClip, transform.position);
            }

            return true;
        }

        /// <summary>
        /// [Animation Event] ฟังก์ชันนี้ถูกเรียกจากหมุดที่ฝังในแอนิเมชันสกิล
        /// ณ เฟรมที่อาวุธฟาดลงพอดี → ดาเมจจะเกิดตรงจุดนี้เท่านั้น (ครั้งเดียว)
        /// </summary>
        public void AE_TriggerSkillEffect()
        {
            if (!hasPendingSkill || pendingSkillData == null)
            {
                Debug.LogWarning("[SkillRunner] Animation Event เรียก AE_TriggerSkillEffect() แต่ไม่มีสกิลค้างอยู่!");
                return;
            }

            // ยิงดาเมจ!
            if (pendingSkillData.effect != null)
            {
                pendingSkillData.effect.Execute(gameObject, pendingSkillData);
#if UNITY_EDITOR
                Debug.Log($"[SkillRunner] ยิงดาเมจสกิล '{pendingSkillData.skillName}' สำเร็จ! (จาก Animation Event)");
#endif
            }

            // เคลียร์สกิลค้าง (ป้องกันยิงซ้ำ = ดาเมจรอบเดียว)
            pendingSkillData = null;
            hasPendingSkill = false;
        }

        /// <summary>
        /// ดึงข้อมูล Cooldown ของสล็อต (สำหรับ UI แสดง Cooldown)
        /// </summary>
        public float GetCooldownProgress(int slotIndex)
        {
            if (skillSlots == null || slotIndex < 0 || slotIndex >= skillSlots.Length) return 0f;
            if (skillSlots[slotIndex] == null) return 0f;
            return skillSlots[slotIndex].cooldown.Progress;
        }

        /// <summary>
        /// ดึง SkillData ของสล็อต (สำหรับ UI แสดงไอคอน/ชื่อ)
        /// </summary>
        public SkillData GetSkillData(int slotIndex)
        {
            if (skillSlots == null || slotIndex < 0 || slotIndex >= skillSlots.Length) return null;
            if (skillSlots[slotIndex] == null) return null;
            return skillSlots[slotIndex].skillData;
        }

        [Header("Debug")]
        [Tooltip("เปิดเพื่อดูระยะสกิลตลอดเวลา (แม้จะไปคลิกไฟล์อื่น)")]
        public bool showSkillGizmos = true;

        // [แก้ไข] เปลี่ยนจาก OnDrawGizmosSelected เป็น OnDrawGizmos ธรรมดา
        // เพื่อให้มันแสดงตลอดเวลาแม้เราจะย้ายไปคลิกไฟล์ ScriptableObject
        private void OnDrawGizmos()
        {
            if (!showSkillGizmos || skillSlots == null) return;

            for (int i = 0; i < skillSlots.Length; i++)
            {
                SkillSlot slot = skillSlots[i];
                if (slot != null && slot.skillData != null && slot.skillData.effect != null)
                {
                    // เรียกฟังก์ชันวาด Gizmos ของแต่ละ Effect
                    slot.skillData.effect.DrawGizmos(transform);
                }
            }
        }
    }
}
