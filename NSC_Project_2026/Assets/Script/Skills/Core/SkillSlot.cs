namespace Skills.Core
{
    /// <summary>
    /// ข้อมูลสกิลที่ถูกติดตั้งในสล็อต (SkillData + Cooldown + ปุ่มกด)
    /// ใช้ [System.Serializable] เพื่อแสดงใน Inspector ได้ (เหมือน Cooldown.cs)
    /// </summary>
    [System.Serializable]
    public class SkillSlot
    {
        [UnityEngine.Tooltip("ลาก SkillData ที่ต้องการใส่ในสล็อตนี้")]
        public SkillData skillData;

        [UnityEngine.Tooltip("ปุ่มที่กดเพื่อใช้สกิลนี้")]
        public UnityEngine.KeyCode activationKey = UnityEngine.KeyCode.Q;

        // Cooldown แยกต่างหากเพราะแต่ละร่างที่สิงอาจมี Cooldown ไม่เหมือนกัน
        [UnityEngine.HideInInspector]
        public Cooldown cooldown = new Cooldown();

        /// <summary>
        /// ซิงค์ค่า Duration จาก SkillData (เรียกตอน Init หรือเปลี่ยนสกิล)
        /// </summary>
        public void SyncCooldown()
        {
            if (skillData != null)
            {
                cooldown.duration = skillData.cooldownDuration;
            }
        }
    }
}
