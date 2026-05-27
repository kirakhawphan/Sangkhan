using UnityEngine;

[CreateAssetMenu(fileName = "NewImpactProfile", menuName = "Combat/Impact Profile")]
public class ImpactProfile : ScriptableObject
{
    [Header("Hit Stop (สำหรับผู้โจมตีและเป้าหมาย)")]
    [Tooltip("ระยะเวลาหยุดเฟรม/หยุดเวลา (วินาที)")]
    public float hitStopDuration = 0.08f;

    [Header("Attacker Feedback (สำหรับคนตี - เฉพาะเมื่อผู้เล่นคุม)")]
    [Tooltip("เปิดใช้การสั่นกล้องของคนตีไหม")]
    public bool enableAttackerShake = true;
    [Tooltip("ความแรงของการสั่นกล้องสำหรับคนตี")]
    public float attackerShakeIntensity = 0.05f;
    [Tooltip("ระยะเวลาการสั่นกล้องสำหรับคนตี")]
    public float attackerShakeDuration = 0.15f;

    [Tooltip("เปิดใช้ FOV Kick สำหรับคนตีไหม")]
    public bool enableAttackerFOVKick = true;
    [Tooltip("ค่า FOV Kick ของคนตี (ลบ = ซูมเข้า, บวก = ซูมออก)")]
    public float attackerFOVKickAmount = -2f;
    [Tooltip("ระยะเวลาของ FOV Kick ของคนตี")]
    public float attackerFOVKickDuration = 0.15f;
    [Tooltip("รูปแบบ Easing ของ FOV Kick ของคนตี")]
    public CameraFOV.EaseType attackerFOVKickEase = CameraFOV.EaseType.Punch;

    [Header("Receiver Feedback (สำหรับคนโดนตี - เฉพาะเมื่อผู้เล่นโดน)")]
    [Tooltip("เปิดใช้การสั่นกล้องของคนโดนตีไหม")]
    public bool enableReceiverShake = true;
    [Tooltip("ความแรงของการสั่นกล้องสำหรับคนโดนตี")]
    public float receiverShakeIntensity = 0.2f;
    [Tooltip("ระยะเวลาการสั่นกล้องสำหรับคนโดนตี")]
    public float receiverShakeDuration = 0.25f;

    [Tooltip("เปิดใช้ FOV Kick สำหรับคนโดนตีไหม")]
    public bool enableReceiverFOVKick = true;
    [Tooltip("ค่า FOV Kick ของคนโดนตี")]
    public float receiverFOVKickAmount = -5f;
    [Tooltip("ระยะเวลาของ FOV Kick ของคนโดนตี")]
    public float receiverFOVKickDuration = 0.2f;
    [Tooltip("รูปแบบ Easing ของ FOV Kick ของคนโดนตี")]
    public CameraFOV.EaseType receiverFOVKickEase = CameraFOV.EaseType.Punch;
}
