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
        // 1. ปิดระบบควบคุมของผู้เล่นทันที
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // 2. เริ่ม Coroutine เพื่อจัดการการตกสู่พื้นอย่างสมจริงก่อนคืนชีพ AI
        StartCoroutine(WaitForGroundAndEnableAI());
    }

    /// <summary>
    /// Coroutine อัจฉริยะสำหรับดึงตัวละครลงพื้นและเปิด AI (ป้องกันบั๊ก NavMesh ลอยกลางอากาศ)
    /// </summary>
    private System.Collections.IEnumerator WaitForGroundAndEnableAI()
    {
        // ขั้นที่ 1: ปล่อยให้ร่างร่วงลงพื้นตามแรงโน้มถ่วง (ใช้ CharacterController)
        if (characterController != null)
        {
            characterController.enabled = true;
            
            float fallTimeout = 3f; // ป้องกันบั๊กลูปอนันต์เผื่อตกแมพ
            
            // ลูปทำงานจนกว่าจะแตะพื้น (isGrounded) หรือหมดเวลา
            while (!characterController.isGrounded && fallTimeout > 0f)
            {
                // SimpleMove(Vector3.zero) จะไม่เดินแนวราบ แต่จะแถมแรงโน้มถ่วง (Gravity) ให้ฟรีๆ
                characterController.SimpleMove(Vector3.zero);
                fallTimeout -= Time.deltaTime;
                yield return null; // รอเฟรมถัดไป
            }

            // พอแตะพื้นแล้ว (หรือหมดเวลา) ให้ปิดทิ้งเพื่อกันไปตีกับ NavMeshAgent
            characterController.enabled = false;
        }

        // ขั้นที่ 2: ดึงพิกัด (Snap) ให้ล็อกติดกับ NavMesh ชัวร์ๆ เผื่อพื้นฐานขรุขระ
        // ค้นหาจุด NavMesh ที่ใกล้ที่สุดในระยะ 2 เมตร
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position; // วาร์ปขยับนิดเดียวให้เท้าติดเนียนๆ
        }

        // ขั้นที่ 3: เปิดระบบเดินของ AI
        if (navAgent != null)
        {
            navAgent.enabled = true;
        }

        // ขั้นที่ 4: เปิดระบบสมอง AI ให้กลับมาไล่ล่าหรือเฝ้ายาม
        if (aiBrain != null)
        {
            aiBrain.enabled = true;
        }

        // ขั้นที่ 5: เปลี่ยน Layer เรดาร์กลับคืนให้กลายเป็นเป้าหมายรอให้คนอื่นมาสิงต่อ
        if (radarTarget != null && possessableLayerCache != -1)
        {
            radarTarget.layer = possessableLayerCache;
        }
    }
}
