using UnityEngine;

public class MedusaController : MonoBehaviour
{
    private Medusa _medusa;
    
    [Header("Movement Settings")]
    public float moveSpeedMultiplier = 2.0f;
    public float thrustSharpness = 2.0f;               

    [Header("Steering & Constraints")]
    public float baseRotationSpeed = 25f;    
    public float maxSteerAnglePerSecond = 12f; // [新增] 限制每秒最大轉向角度
    [Range(0f, 1f)]
    public float turnSpeedReduction = 0.2f;    // [新增] 轉向時速度衰減係數 (值越小減速越多)

    public void Initialize(Medusa medusa)
    {
        _medusa = medusa;
        this.moveSpeedMultiplier = medusa.moveSpeedMultiplier;
    }

    public void UpdateMovement(float dt)
    {
        if (!_medusa.isReady || FoodManager.Instance == null) return;

        Vector3 targetPos = FoodManager.Instance.currentFoodPosition;
        float rawSine = Mathf.Sin(_medusa.phase + 4.4f);
        bool isThrusting = rawSine > 0;

        // --- 1. 計算轉向與角度約束 ---
        float turnFactor = 0f; // 用來記錄轉向的程度，供後續減速使用
        HandleRestrictedSteering(targetPos, dt, rawSine, isThrusting, out turnFactor);

        // --- 2. 計算推進位移 (包含轉向減速) ---
        float adjustedThrust = isThrusting ? Mathf.Pow(rawSine, thrustSharpness) : rawSine * 0.05f;
        
        // 基礎速度計算
        float speed = (0.5f + adjustedThrust + _medusa.charge * 1.5f);
        
        // [新增] 轉向減速邏輯：當轉向角度越大，速度衰減越多
        // turnFactor 為 0~1，代表當前轉向動作的強度
        float speedPenalty = Mathf.Lerp(1.0f, turnSpeedReduction, turnFactor);
        speed *= speedPenalty;

        // 應用位移
        transform.position += transform.up * (speed * dt * moveSpeedMultiplier);

        // --- 3. 邊界與邏輯檢查 ---
        FoodManager.Instance.CheckIfReached(transform.position);
        if (transform.position.magnitude > 55f) _medusa.ResetPosition();
    }

    private void HandleRestrictedSteering(Vector3 targetPos, float dt, float rawSine, bool isThrusting, out float turnFactor)
    {
        turnFactor = 0f;
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir == Vector3.zero) return;

        // 計算目前方向與目標方向的夾角
        float angleToTarget = Vector3.Angle(transform.up, dir);
        if (angleToTarget < 0.1f) return;

        // 1. 決定基礎轉向能力 (隨相位改變)
        float steerAbility = isThrusting ? (0.2f + rawSine * 0.8f) : 0.1f;
        
        // 2. 應用最大角度限制 (24度/秒)
        // 我們取「設定的轉向速度」與「硬上限」的最小值
        float currentSteerSpeed = Mathf.Min(baseRotationSpeed * steerAbility, maxSteerAnglePerSecond);

        // 3. 執行旋轉
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, dir);
        Quaternion nextRot = Quaternion.RotateTowards(transform.rotation, targetRot, currentSteerSpeed * dt);
        
        // 計算這一幀實際轉動了幾度，用來輸出 turnFactor
        float actualFrameAngle = Quaternion.Angle(transform.rotation, nextRot);
        turnFactor = Mathf.Clamp01(actualFrameAngle / (maxSteerAnglePerSecond * dt));

        transform.rotation = nextRot;
    }
}