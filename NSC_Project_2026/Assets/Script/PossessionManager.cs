using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ตัวละครที่ผู้เล่นกำลังควบคุมอยู่
public class PossessionManager : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("ระยะการตรวจจับเป้าหมายที่อยู่หน้ากล้อง (ความไกล)")]
    public float maxDetectionDistance = 15f;
    
    [Tooltip("ความกว้างของเป้าเล็ง (ยิ่งมากยิ่งเล็งโดนง่ายโดยไม่ต้องหันหน้าตรงเป๊ะ)")]
    public float aimRadius = 1.5f;

    [Tooltip("Layer ของเป้าหมาย (ใช้สำหรับจำกัดการค้นหาให้มีประสิทธิภาพขึ้น ควรตั้ง Layer ให้กับตัวที่สิงได้)")]
    public LayerMask possessableLayer; 

    [Tooltip("กล้องของผู้เล่น (ถ้าไม่ใส่สคริปต์จะหา Camera.main ให้อัตโนมัติ)")]
    public Camera playerCamera;

    [Header("Target Info (Read Only)")]
    [Tooltip("เป้าหมายที่อยู่หน้ากล้องและใกล้ที่สุดในขณะนี้")]
    public PossessableEntity currentTarget;

    void Start()
    {
        // ถ้าไม่ได้ลากกล้องมาใส่ใน Inspector ให้หาจาก Main Camera อัตโนมัติ
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    void Update()
    {
        // 1. ตรวจจับเป้าหมายแบบหน้ากล้องเรียลไทม์
        DetectPossessableEntities();

        // 2. รับ Input จากผู้เล่น (ตัวอย่างใช้ปุ่ม E สำหรับสิงร่าง)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentTarget != null)
            {
                Possess(currentTarget);
            }
            else
            {
                Debug.Log("ไม่มีเป้าหมายให้สิงร่างในเป้าเล็ง!");
            }
        }
    }

    // ฟังก์ชันสำหรับตรวจหา PossessableEntity ตรงหน้ากล้อง
    void DetectPossessableEntities()
    {
        if (playerCamera == null) return;

        // สร้างเส้นเล็ง (Ray) ยิงออกจากตรงกลางหน้าจอของกล้อง
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        
        // ใช้ SphereCastAll ยิงเหมือน "แคปซูล" พุ่งไปข้างหน้ากล้อง
        RaycastHit[] hits;
        
        if (possessableLayer != 0)
        {
            hits = Physics.SphereCastAll(ray, aimRadius, maxDetectionDistance, possessableLayer);
        }
        else
        {
            hits = Physics.SphereCastAll(ray, aimRadius, maxDetectionDistance);
        }
        
        PossessableEntity closestEntity = null;
        float closestDistance = Mathf.Infinity;

        // วนลูปหาตัวที่อยู่ใกล้กล้องมากที่สุดในบรรดาตัวที่โดนลำแสง
        foreach (RaycastHit hit in hits)
        {
            PossessableEntity entity = hit.collider.GetComponent<PossessableEntity>();
            
            // ตรวจสอบว่ามีสคริปต์ PossessableEntity และไม่ใช่ตัวเอง (กันการสิงร่างตัวเอง)
            if (entity != null && entity.gameObject != this.gameObject)
            {
                // เช็คระยะว่าใครใกล้กว่า
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestEntity = entity;
                }
            }
        }

        // อัปเดตเป้าหมายปัจจุบัน
        currentTarget = closestEntity;
    }

    // ฟังก์ชันสำหรับสิงร่าง
    void Possess(PossessableEntity targetEntity)
    {
        Debug.Log("กำลังสิงร่าง: " + targetEntity.entityName);
        
        // --- สิ่งที่ต้องทำต่อไปในระบบสลับร่าง ---
        // 1. ปิดระบบควบคุมของร่างเดิม (เช่น GetComponent<PlayerMovement>().enabled = false;)
        // 2. เปิดระบบควบคุมให้ร่างใหม่ (targetEntity.GetComponent<PlayerMovement>().enabled = true;)
        // 3. ย้ายมุมกล้อง Camera ไปยังร่างใหม่
        // 4. (ทางเลือก) ย้ายสคริปต์ PossessionManager นี้ไปไว้ที่ร่างใหม่ เพื่อให้ร่างใหม่ไปสิงคนอื่นต่อได้
    }

    // แสดงลำแสงการตรวจจับสีเหลืองในหน้า Scene (ช่วยให้ตั้งค่า aimRadius ได้ง่ายขึ้น)
    private void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            // วาดเส้นและวงกลมเพื่อจำลองลำแสง
            Gizmos.DrawRay(ray.origin, ray.direction * maxDetectionDistance);
            Gizmos.DrawWireSphere(ray.origin + (ray.direction * maxDetectionDistance), aimRadius);
        }
    }
}
