using UnityEngine;

public class MedusaLookAtCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;        // 拖入你的 Medusa GameObject

    [Header("Settings")]
    public float turnSpeed = 5f;    // 轉動的平滑速度 (數值越大轉越快)
    
    // 如果你希望相機永遠保持水平（不要歪頭），這個選項很有用
    public bool lockRotationZ = true; 

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 計算目標方向向量 (目標位置 - 相機位置)
        Vector3 direction = target.position - transform.position;

        // 如果距離太近，避免計算錯誤
        if (direction.sqrMagnitude < 0.001f) return;

        // 2. 計算目標旋轉角度 (LookRotation)
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // 3. 平滑插值 (Slerp) 讓轉動有重量感，不是死板的鎖定
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

        // 4. (選用) 鎖定 Z 軸旋轉，防止相機在劇烈角度時產生「歪頭」現象
        if (lockRotationZ)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(euler.x, euler.y, 0f);
        }
    }
}