using UnityEngine;
using UnityEngine.UI;

public class PGBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("อ้างอิงไปยังระบบ PG (ลาก Object ที่มี PGController มาใส่)")]
    [SerializeField] private PGController targetPGController;
    
    [Tooltip("อ้างอิงไปยัง UI Image ที่ทำหน้าที่เป็นหลอด PG")]
    [SerializeField] private Image pgFillImage;

    [Header("Settings")]
    [Tooltip("ความเร็วในการไหลของหลอด (Smooth Drain)")]
    [SerializeField] private float smoothDrainSpeed = 5f;

    private float targetPercentage = 1f;

    private void OnEnable()
    {
        if (targetPGController != null)
        {
            targetPGController.OnPGChanged += HandlePGChanged;
            
            // Snap ค่าเริ่มต้นทันที
            targetPercentage = targetPGController.CurrentPGPercentage;
            if (pgFillImage != null)
            {
                pgFillImage.fillAmount = targetPercentage;
            }
        }
    }

    private void OnDisable()
    {
        if (targetPGController != null)
        {
            targetPGController.OnPGChanged -= HandlePGChanged;
        }
    }

    private void Start()
    {
        // หากไม่ได้ลากใส่ไว้ใน Inspector ให้ลองค้นหาอัตโนมัติ
        if (targetPGController == null)
        {
            targetPGController = FindFirstObjectByType<PGController>();
            if (targetPGController != null)
            {
                targetPGController.OnPGChanged += HandlePGChanged;
                targetPercentage = targetPGController.CurrentPGPercentage;
                if (pgFillImage != null)
                {
                    pgFillImage.fillAmount = targetPercentage;
                }
            }
        }
    }

    private void HandlePGChanged(float pgPercentage)
    {
        // รับค่าเปอร์เซ็นต์ (0.0 - 1.0) มาจาก PGController
        targetPercentage = pgPercentage;
    }

    private void Update()
    {
        if (pgFillImage == null) return;

        // อัปเดตหลอด PG แบบ Smooth
        if (Mathf.Abs(pgFillImage.fillAmount - targetPercentage) > 0.001f)
        {
            pgFillImage.fillAmount = Mathf.Lerp(pgFillImage.fillAmount, targetPercentage, Time.deltaTime * smoothDrainSpeed);
        }
        else
        {
            pgFillImage.fillAmount = targetPercentage;
        }
    }
}
