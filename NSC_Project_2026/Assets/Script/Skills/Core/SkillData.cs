using UnityEngine;

namespace Skills.Core
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "Skills/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillName = "New Skill";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Cooldown")]
        public float cooldownDuration = 5f;

        [Header("Animation")]
        [Tooltip("ชื่อ Trigger ใน Animator ที่จะเล่นเมื่อใช้สกิล")]
        public string animationTrigger = "Skill";

        [Tooltip("ระยะเวลาล็อกการเคลื่อนที่ขณะเล่นสกิล (วินาที)")]
        public float movementLockTime = 0.5f;

        [Header("Effect")]
        [Tooltip("ลอจิกของสกิลนี้ (ลาก ScriptableObject ที่สืบทอดจาก SkillEffect มาใส่)")]
        public SkillEffect effect;

        [Header("VFX / SFX")]
        public GameObject vfxPrefab;
        public AudioClip sfxClip;
    }
}
