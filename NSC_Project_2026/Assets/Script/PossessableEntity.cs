using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ GameObject ตัวละครอื่นๆ ที่ต้องการให้ผู้เล่นสามารถสิงร่างได้
public class PossessableEntity : MonoBehaviour
{
    [Header("Entity Info")]
    [Tooltip("ชื่อของร่างนี้ (เอาไว้แสดงผล UI หรือใช้แยกแยะ)")]
    public string entityName = "Unknown Entity";
    
    // ตัวแปรต่างๆ ที่คุณอาจจะต้องเพิ่มในอนาคต เช่น:
    // public Transform cameraFollowPoint;
    // public MonoBehaviour characterControllerScript;
}
