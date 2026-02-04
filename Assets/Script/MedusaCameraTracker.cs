using UnityEngine;

public class MedusaFixedCamera : MonoBehaviour
{
    [Header("Target")]
    public Medusa medusaTarget;     

    [Header("Positioning")]
    public Vector3 fixedPosition = new Vector3(0, 0, -45); 
    public bool useInitialPositionAsFixed = true;

    [Header("Look At Settings")]
    public float rotationSmooth = 3.0f; // 轉向對準速度
    public Vector3 lookOffset = Vector3.zero;

    [Header("Proportional FOV (Size Control)")]
    [Tooltip("此值越大，水母在螢幕中看起來就越大")]
    public float sizeConstant = 1500f;  // 用來維持比例的常數 (FOV = sizeConstant / distance)
    public float minFOV = 15f;          // 望遠極限 (最遠時的特寫)
    public float maxFOV = 90f;          // 廣角極限 (最近時的防爆框)
    public float fovSmoothTime = 0.2f;

    private Camera _cam;
    private float _fovVelocity;

    void Start()
    {
        _cam = GetComponent<Camera>();
        if (useInitialPositionAsFixed) fixedPosition = transform.position;
        else transform.position = fixedPosition;
    }

    void LateUpdate()
    {
        if (medusaTarget == null || !medusaTarget.isReady) return;

        Transform t = medusaTarget.transform;

        // 1. 固定相機位置
        transform.position = fixedPosition;

        // 2. 平滑追蹤水母
        Vector3 targetPoint = t.position + lookOffset;
        Vector3 dir = targetPoint - transform.position;
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmooth * Time.deltaTime);
        }

        // 3. 【核心修正】比例維持 FOV 邏輯
        float distance = Vector3.Distance(transform.position, t.position);
        
        // 公式：FOV 與距離成反比。距離增加時，FOV 減小（望遠），以維持物體大小
        // 1500 是根據半徑 50 的環境球測試的一個建議值
        float targetFOV = sizeConstant / distance;

        // 限制在合理範圍內，避免距離過近或過遠時 FOV 爆炸
        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);

        _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, targetFOV, ref _fovVelocity, fovSmoothTime);
    }
}