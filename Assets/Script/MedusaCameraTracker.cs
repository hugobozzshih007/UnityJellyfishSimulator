using UnityEngine;

public class MedusaCameraTracker : MonoBehaviour
{
    [Header("Target")]
    public Medusa medusaTarget;     
    
    [Header("Positioning")]
    public float distance = 12f;      // 相機與水母的距離
    public float heightOffset = 2f;   // 垂直高度
    public float lateralOffset = 6f;  // 側面偏移量 (關鍵：產生側面感的數值)
    public float bodyCenterOffset = 5f; // 向後偏移量，將重心移向觸手

    [Header("Follow Dynamics")]
    [Range(0.1f, 1.0f)]
    public float followSmoothTime = 0.5f; // 位移延遲 (數值越大，水母衝出去的感覺越強)
    public float rotationSmooth = .5f;    // 旋轉延遲 (數值小，轉彎時會拍到更多側面)
    
    private Vector3 _currentVelocity;
    private Vector3 _sideDirection;

    void Start()
    {
        // 初始時隨機選擇從左側還是右側觀看
        _sideDirection = Random.value > 0.5f ? Vector3.right : Vector3.left;
    }

    void LateUpdate()
    {
        if (medusaTarget == null || !medusaTarget.isReady) return;

        Transform t = medusaTarget.transform;

        // 1. 定義「視覺重心」(包含觸手在內的中心點)
        // 從頭部座標沿著反前進方向偏移，讓整體水母對齊畫面中心
        Vector3 visualCenter = t.position - (t.up * bodyCenterOffset);

        // 2. 計算側面與後方的混合向量
        // 我們不使用固定的座標，而是根據水母的旋轉動態計算側向向量
        Vector3 rightVec = Vector3.Cross(t.up, Vector3.up).normalized;
        if (rightVec.sqrMagnitude < 0.01f) rightVec = t.right; // 避免萬向鎖

        // 目標位置 = 重心點 + (反向拉開) + (向上拉開) + (向側邊拉開)
        Vector3 desiredPos = visualCenter 
                           - (t.up * distance * 0.25f) 
                           + (Vector3.up * heightOffset) 
                           + (rightVec * lateralOffset * (Vector3.Dot(_sideDirection, Vector3.right) > 0 ? 1 : -1));

        // 3. 實作延遲跟隨 (SmoothDamp)
        // 這是解決「水母前進後相機再跟上」的關鍵邏輯
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPos, 
            ref _currentVelocity, 
            followSmoothTime
        );

        // 4. 處理電影感旋轉
        // 相機看向視覺重心 (visualCenter)，因為相機在側後方，會拍到極具張力的側面
        Vector3 lookDir = visualCenter - transform.position;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            
            // 使用較慢的 Slerp，當水母轉彎時，鏡頭會「跟不上」，從而自動捕捉到側視角
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRot, 
                rotationSmooth * Time.deltaTime
            );
        }

        // 5. 處理傳送 (Teleport) 避免畫面飛掠
        if (Vector3.Distance(transform.position, t.position) > 60f)
        {
            transform.position = desiredPos;
            _currentVelocity = Vector3.zero;
        }
    }
}