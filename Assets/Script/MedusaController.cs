using UnityEngine;

public class MedusaController : MonoBehaviour
{
    private Medusa _medusa;
    
    [Header("Movement Settings")]
    public float moveSpeedMultiplier = 2.0f;
    [Range(0f, 1f)] public float glideResistance = 0.05f; 
    public float thrustSharpness = 3.0f;               

    [Header("Steering Settings")]
    public float baseRotationSpeed = 40f;    // 基礎轉向速度 (調低以產生弧線)
    [Range(0f, 1f)] public float glideSteerFactor = 0.1f; 

    public void Initialize(Medusa medusa)
    {
        _medusa = medusa;
        this.moveSpeedMultiplier = medusa.moveSpeedMultiplier;
    }

    public void UpdateMovement(float dt)
    {
        if (!_medusa.isReady || FoodManager.Instance == null) return;

        Vector3 targetPos = FoodManager.Instance.currentFoodPosition;

        // 1. 計算相位狀態
        float rawSine = Mathf.Sin(_medusa.phase + 4.4f);
        bool isThrusting = rawSine > 0;

        // 2. 處理弧線轉向 (將轉向稀釋到前進過程中)
        HandleArcSteering(targetPos, dt, rawSine, isThrusting);

        // 3. 計算推進位移 (加大落差感)
        float adjustedThrust = isThrusting ? Mathf.Pow(rawSine, thrustSharpness) : rawSine * glideResistance;
        float thrust = 0.5f + adjustedThrust + _medusa.charge * 1.5f;
        float speed = thrust * dt * moveSpeedMultiplier;

        // 4. 混合位移方向：讓位移路徑稍微帶有一點點目標的引力，增加弧線感
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        Vector3 moveDirection = Vector3.Slerp(transform.up, dirToTarget, 0.05f);

        transform.position += moveDirection * speed;

        // 5. 邏輯檢查
        FoodManager.Instance.CheckIfReached(transform.position);

        // 邊界安全重置 (如果超出環境球 50 單位)
        if (transform.position.magnitude > 55f)
        {
            _medusa.ResetPosition();
        }
    }

    private void HandleArcSteering(Vector3 targetPos, float dt, float rawSine, bool isThrusting)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, dir);

        // 關鍵修正：轉向速度與推進強度掛鉤
        // 在噴射最強的瞬間(rawSine=1) 轉向最快，滑行時幾乎不轉
        float currentSteerSpeed = baseRotationSpeed;
        if (isThrusting)
            currentSteerSpeed *= (0.2f + rawSine * 0.8f);
        else
            currentSteerSpeed *= glideSteerFactor;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, 
            targetRot, 
            currentSteerSpeed * dt
        );
    }
}