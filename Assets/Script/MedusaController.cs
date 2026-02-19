using UnityEngine;

public class MedusaController : MonoBehaviour
{
    private Medusa _medusa;
    private float _currentTurnFactor = 0f; // [新增] 儲存當前轉向強度
    
    [Header("Movement Settings")]
    public float moveSpeedMultiplier = 2.0f;
    public float thrustSharpness = 2.0f;               

    [Header("Steering & Constraints")]
    public float baseRotationSpeed = 25f;    
    public float maxSteerAnglePerSecond = 12f; 
    [Range(0f, 1f)]
    public float turnSpeedReduction = 1f;    

    public void Initialize(Medusa medusa)
    {
        _medusa = medusa;
        this.moveSpeedMultiplier = medusa.config.moveSpeedMultiplier;
    }

    // [新增] 供 Medusa.cs 讀取轉向強度以影響擺動頻率
    public float GetTurnFactor() => _currentTurnFactor;

    public void UpdateMovement(float dt)
    {
        if (!_medusa.isReady || FoodManager.Instance == null) return;

        Vector3 targetPos = FoodManager.Instance.currentFoodPosition;
        
        // 此處 phase 已在 Medusa.cs 中根據轉向速度加速更新
        float rawSine = Mathf.Sin(_medusa.phase + 4.4f);
        bool isThrusting = rawSine > 0;

        // --- 1. 計算轉向與角度約束 ---
        HandleRestrictedSteering(targetPos, dt, rawSine, isThrusting, out _currentTurnFactor);

        // --- 2. 計算推進位移 (包含轉向減速) ---
        float adjustedThrust = isThrusting ? Mathf.Pow(rawSine, thrustSharpness) : rawSine * 0.05f;
        float speed = (0.5f + adjustedThrust + _medusa.charge * 1.5f);
        
        float speedPenalty = Mathf.Lerp(1.0f, turnSpeedReduction, _currentTurnFactor);
        speed *= speedPenalty;

        transform.position += transform.up * (speed * dt * moveSpeedMultiplier);

        // --- 3. 邊界與邏輯檢查 ---
        FoodManager.Instance.CheckIfReached(transform.position);
        if (transform.position.magnitude > 55f) _medusa.ResetPosition();
    }

    private void HandleRestrictedSteering(Vector3 targetPos, float dt, float rawSine, bool isThrusting, out float turnFactor)
    {
        turnFactor = 0f;
        Vector3 toTarget = targetPos - transform.position;
        Vector3 dir = toTarget.normalized;
        if (dir == Vector3.zero) return;

        // --- 1. 偵測並避開垂直向下 (Hack) ---
        // 當目標過於垂直時，加入水平偏移強迫水母走弧線，避免物理塌陷 
        if (dir.y < -0.98f) 
        {
            Vector3 pushOffset = transform.forward; 
            pushOffset.y = 0; 
            if (pushOffset.sqrMagnitude < 0.01f) pushOffset = transform.right;
            dir = Vector3.Normalize(dir + pushOffset.normalized * 0.5f);
        }

        // --- 2. 計算剩餘轉向角度 ---
        float angleToTarget = Vector3.Angle(transform.up, dir);

        // 設定一個閾值（例如 0.5 度），小於此角度視為轉向完成
        if (angleToTarget < 0.5f) 
        {
            _currentTurnFactor = 0f;
            turnFactor = 0f;
            return;
        }

        // --- 3. 執行旋轉運算 ---
        float steerAbility = isThrusting ? (0.2f + rawSine * 0.8f) : 0.1f;
        float currentSteerSpeed = Mathf.Min(baseRotationSpeed * steerAbility, maxSteerAnglePerSecond);

        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, currentSteerSpeed * dt);

        // --- 4. 關鍵修改：穩定輸出 turnFactor ---
        // 我們改用「剩餘角度」除以一個參考值（例如 45 度）來計算強度
        // 這樣在整個轉彎過程中，turnFactor 會平滑且持續地存在，直到對準目標為止
        // 基礎邏輯參考自：
        turnFactor = Mathf.Clamp01(angleToTarget / 45f); 
    
        // 更新類別成員變數，確保 Medusa.cs 讀取到的是持續的加速狀態
        _currentTurnFactor = turnFactor; 
    }
}