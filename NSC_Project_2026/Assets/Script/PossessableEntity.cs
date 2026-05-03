using UnityEngine;
using UnityEngine.AI;

// นำสคริปต์นี้ไปแปะไว้ที่ GameObject ตัวละครอื่นๆ ที่ต้องการให้ผู้เล่นสามารถสิงร่างได้
public class PossessableEntity : MonoBehaviour
{
    [Header("Entity Info")]
    [SerializeField, Tooltip("ชื่อของร่างนี้ (เอาไว้แสดงผล UI หรือใช้แยกแยะ)")]
    private string entityName = "Unknown Entity";
    
    // Property สำหรับให้ภายนอกอ่านค่าได้อย่างเดียว (Encapsulation)
    public string EntityName => entityName;

    [Header("Possession Switch Components")]
    [SerializeField, Tooltip("ระบบสมอง AI ที่ควบคุมพฤติกรรม")]
    private EnemyBrain aiBrain;

    [SerializeField, Tooltip("ระบบหาเส้นทางของ AI")]
    private NavMeshAgent navAgent;

    [SerializeField, Tooltip("ระบบควบคุมตัวละครของผู้เล่น")]
    private MonoBehaviour playerController;

    [SerializeField, Tooltip("ระบบฟิสิกส์การชนและการเดินของผู้เล่น (ถ้ามี)")]
    private CharacterController characterController;

    [Header("Targeting")]
    [SerializeField, Tooltip("GameObject เปล่าๆ ที่มี Collider รับเลเซอร์เรดาร์")]
    private GameObject radarTarget;

    // --- แคชค่า Layer ไว้ใช้งาน (Performance Optimization) ---
    private int playerLayerCache = -1;
    private int possessableLayerCache = -1;

    private void Awake()
    {
        // ดึงค่า Layer ออกมาเก็บไว้ในตัวแปรแค่ครั้งเดียวตอนเริ่มเกม เพื่อไม่ต้องเรียก NameToLayer ซ้ำๆ
        playerLayerCache = LayerMask.NameToLayer("Player");
        possessableLayerCache = LayerMask.NameToLayer("Possessable");

        if (playerLayerCache == -1)
            Debug.LogWarning("[PossessableEntity] ไม่พบ Layer ที่ชื่อ 'Player' ในโปรเจกต์!", this);
            
        if (possessableLayerCache == -1)
            Debug.LogWarning("[PossessableEntity] ไม่พบ Layer ที่ชื่อ 'Possessable' ในโปรเจกต์!", this);
    }

    /// <summary>
    /// ฟังก์ชันทำงานเมื่อผู้เล่นเข้ามาสิงร่างนี้
    /// </summary>
    public void OnPossessed()
    {
        // 1. ปิดระบบสมอง AI (enabled = false)
        if (aiBrain != null)
        {
            aiBrain.enabled = false;
        }

        // 2. ปิด NavMeshAgent เพื่อไม่ให้แย่งผู้เล่นเดิน
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // 3. เปิดระบบฟิสิกส์ CharacterController ให้ผู้เล่นใช้เดิน
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // 4. เปิดระบบควบคุมของผู้เล่น (enabled = true)
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // 5. เปลี่ยน Layer ของเป้าหมายเรดาร์เป็น "Player"
        if (radarTarget != null && playerLayerCache != -1)
        {
            radarTarget.layer = playerLayerCache;
        }
    }

    /// <summary>
    /// ฟังก์ชันทำงานเมื่อผู้เล่นออกจากร่างนี้ (ดึงปลั๊ก)
    /// </summary>
    public void OnUnpossessed()
    {
        // 1. ปิดระบบควบคุมของผู้เล่น
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // 2. ปิดระบบฟิสิกส์ CharacterController ไม่ให้แย่ง AI เดิน
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // 3. เปิด NavMeshAgent เพื่อเตรียมให้ AI กลับมาใช้งาน
        if (navAgent != null)
        {
            navAgent.enabled = true;
        }

        // 4. เปิดระบบสมอง AI ให้กลับมาคิดและทำงาน
        if (aiBrain != null)
        {
            aiBrain.enabled = true;
        }

        // 5. เปลี่ยน Layer ของเป้าหมายเรดาร์กลับเป็น "Possessable"
        if (radarTarget != null && possessableLayerCache != -1)
        {
            radarTarget.layer = possessableLayerCache;
        }
    }
}
