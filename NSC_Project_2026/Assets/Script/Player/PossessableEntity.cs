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

    [SerializeField, Tooltip("ระบบต่อสู้ของผู้เล่น (เปิดตอนสิงร่าง, ปิดตอนออก)")]
    private PlayerCombat playerCombat;

    [SerializeField, Tooltip("ระบบฟิสิกส์การชนและการเดินของผู้เล่น (ถ้ามี)")]
    private CharacterController characterController;

    [Header("Targeting")]
    [SerializeField, Tooltip("GameObject เปล่าๆ ที่มี Collider รับเลเซอร์เรดาร์")]
    private GameObject radarTarget;

    [Header("Hitbox Layer Switching")]
    [SerializeField, Tooltip("Layer ของเป้าหมายที่ร่างนี้จะโจมตีเมื่อ 'ผู้เล่น' กำลังสิง (ปกติคือ Possessable/Enemy)")]
    private LayerMask attackLayerWhenPossessed;
    
    [SerializeField, Tooltip("Layer ของเป้าหมายที่ร่างนี้จะโจมตีเมื่อ 'เป็น AI' (ปกติคือ Player)")]
    private LayerMask attackLayerWhenAI;

    // --- แคชค่า Layer ไว้ใช้งาน (Performance Optimization) ---
    private int playerLayerCache = -1;
    private int possessableLayerCache = -1;
    private int originalBodyLayer = -1; // [เพิ่ม] จำ Layer ดั้งเดิมของตัวเองไว้

    private Coroutine unpossessCoroutine;

    private void Awake()
    {
        // จำ Layer ดั้งเดิมของตัวเองตั้งแต่เริ่มเกม
        originalBodyLayer = gameObject.layer;

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
        // ยกเลิกการคืนชีพ AI ถ้าเราสิงกลับมาทันที
        if (unpossessCoroutine != null)
        {
            StopCoroutine(unpossessCoroutine);
            unpossessCoroutine = null;
        }

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

        // 5. [เพิ่ม] เปิดระบบต่อสู้ของผู้เล่น
        if (playerCombat != null)
        {
            playerCombat.enabled = true;
        }

        // 5. เปลี่ยน Layer ของเป้าหมายเรดาร์เป็น "Player"
        if (radarTarget != null && playerLayerCache != -1)
        {
            radarTarget.layer = playerLayerCache;
        }

        // 6. เปลี่ยน Layer ของตัวเอง (เพื่อรับดาเมจจากศัตรูตัวอื่น)
        if (playerLayerCache != -1)
        {
            gameObject.layer = playerLayerCache;
        }

        // 7. สลับเป้าหมายของ Hitbox ให้อาวุธหันไปตีศัตรูแทน
        MeleeHitbox[] hitboxes = GetComponentsInChildren<MeleeHitbox>(true);
        for (int i = 0; i < hitboxes.Length; i++)
        {
            hitboxes[i].SetTargetLayer(attackLayerWhenPossessed);
        }

        Debug.Log($"[Possession] เข้าสิงร่าง: '{gameObject.name}' | อัปเดต Hitbox ให้เป้าหมายเป็น LayerMask {attackLayerWhenPossessed.value} สำเร็จ");
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

        // 2. [เพิ่ม] ปิดระบบต่อสู้ของผู้เล่น
        if (playerCombat != null)
        {
            playerCombat.enabled = false;
        }

        // 2. เปลี่ยน Layer เรดาร์กลับคืนให้กลายเป็นเป้าหมายรอให้คนอื่นมาสิงต่อ
        if (radarTarget != null && possessableLayerCache != -1)
        {
            radarTarget.layer = possessableLayerCache;
        }

        // 3. เปลี่ยน Layer ของตัวเองกลับเป็นร่างธรรมดาดั้งเดิม (เช่น Enemy หรืออะไรก็ตามที่ตั้งไว้แต่แรก)
        if (originalBodyLayer != -1)
        {
            gameObject.layer = originalBodyLayer;
        }

        // 4. คืนค่าให้ Hitbox กลับมาตีผู้เล่นเหมือนเดิม
        MeleeHitbox[] hitboxes = GetComponentsInChildren<MeleeHitbox>(true);
        for (int i = 0; i < hitboxes.Length; i++)
        {
            hitboxes[i].SetTargetLayer(attackLayerWhenAI);
        }

        Debug.Log($"[Possession] ถอนวิญญาณออกจากร่าง: '{gameObject.name}' | อัปเดต Hitbox ให้เป้าหมายเป็น LayerMask {attackLayerWhenAI.value} สำเร็จ");

        // 5. เริ่ม Coroutine เพื่อจัดการการตกสู่พื้นอย่างสมจริงก่อนคืนชีพ AI
        unpossessCoroutine = StartCoroutine(WaitForGroundAndEnableAI());
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

            // [ลบออก] เราจะไม่ปิด CharacterController แล้ว 
            // เพราะต้องการให้ AI มี 'ร่างกาย' ไว้ชนกับผู้เล่น และใช้เดินด้วย NavMesh Sync
            // characterController.enabled = false;
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
        
        unpossessCoroutine = null;
    }
}
