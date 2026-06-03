using UnityEngine;

namespace Skills.Core
{
    /// <summary>
    /// พิมพ์เขียว (Abstract Base Class) สำหรับ Logic ของสกิลทุกประเภท
    /// สร้างเป็น ScriptableObject เพื่อให้ลากใส่ช่อง SkillData.effect ใน Inspector ได้
    /// </summary>
    public abstract class SkillEffect : ScriptableObject
    {
        [Header("Debug")]
        [Tooltip("สีของเส้น Gizmos ที่วาดใน Scene View")]
        public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.8f); // สีส้มเด่นๆ ค่าเริ่มต้น

        /// <summary>
        /// ฟังก์ชันทำงานจริงเมื่อสกิลถูกใช้
        /// </summary>
        /// <param name="caster">ร่างที่กำลังใช้สกิล (ตัวที่ผู้เล่นสิงอยู่)</param>
        /// <param name="skillData">ข้อมูลสกิลที่ใช้ (สำหรับดึงค่าดาเมจ, VFX ฯลฯ)</param>
        public abstract void Execute(GameObject caster, SkillData skillData);

        /// <summary>
        /// [เพิ่ม] ฟังก์ชันสำหรับวาดเส้น Hitbox หรือระยะสกิลใน Scene View เพื่อให้มองเห็น
        /// </summary>
        public virtual void DrawGizmos(Transform caster)
        {
            // ให้ Effect แต่ละประเภท Override เพื่อวาดรูปทรงของตัวเอง
        }
    }
}
