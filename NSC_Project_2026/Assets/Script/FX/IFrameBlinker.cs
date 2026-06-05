using System.Collections;
using UnityEngine;

// สคริปต์ Visual Feedback ที่ทำให้ตัวละครกระพริบระหว่างที่อยู่ในสถานะ I-Frame
public class IFrameBlinker : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private Renderer[] targetRenderers;

    private Coroutine blinkCoroutine;

    private void Awake()
    {
        if (healthSystem == null)
        {
            healthSystem = GetComponentInParent<HealthSystem>();
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnIFrameStateChanged += HandleIFrameStateChanged;
        }
    }

    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.OnIFrameStateChanged -= HandleIFrameStateChanged;
        }
        
        // แน่ใจว่าตอนปิดสคริปต์จะทำให้ Renderer แสดงปกติเสมอ
        StopBlinking();
    }

    private void HandleIFrameStateChanged(bool isInvincible)
    {
        if (isInvincible)
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
            }
            blinkCoroutine = StartCoroutine(BlinkRoutine());
        }
        else
        {
            StopBlinking();
        }
    }

    private IEnumerator BlinkRoutine()
    {
        bool isVisible = true;
        
        while (true)
        {
            isVisible = !isVisible;
            SetRenderersVisible(isVisible);
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    private void StopBlinking()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        SetRenderersVisible(true);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (targetRenderers == null) return;
        
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].enabled = visible;
            }
        }
    }
}
