using UnityEngine;

public class MedusaCameraTracker : MonoBehaviour
{
    [Header("Target")]
    public Transform target;        // 拖入你的 Medusa GameObject
    public Vector3 offset;          // 相機與水母的相對距離 (留空會自動計算)
    
    [Header("Settings")]
    public float smoothSpeed = 5f;  // 跟隨的平滑度 (數值越大越緊)
    public bool autoOffset = true;  // 是否在開始時自動抓取目前的相對距離
    
    [Header("Teleport Handling")]
    public float teleportThreshold = 10.0f; // 如果目標瞬間移動超過這距離，相機就直接瞬移

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("Camera has no target!");
            return;
        }

        // 如果開啟自動計算，就以目前的場景擺設作為標準偏移量
        if (autoOffset)
        {
            offset = transform.position - target.position;
        }
    }

    // 使用 LateUpdate 確保在水母的所有 Update (移動) 完成後才移動相機
    // 這樣可以完全消除畫面抖動 (Jitter)
    void LateUpdate()
    {
        if (target == null) return;

        // 計算目標位置
        Vector3 desiredPosition = target.position + offset;

        // 檢查是否發生了 "傳送" (例如水母從頂部回到底部)
        float dist = Vector3.Distance(transform.position, desiredPosition);
        
        if (dist > teleportThreshold)
        {
            // 如果距離太遠，判定為傳送，直接瞬移過去 (避免畫面快速飛過)
            transform.position = desiredPosition;
        }
        else
        {
            // 否則進行平滑移動
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
        
        // 如果你也希望相機旋轉跟著水母轉 (通常水母這類生物不需要，保持水平比較好看)
        // transform.LookAt(target); 
    }
}