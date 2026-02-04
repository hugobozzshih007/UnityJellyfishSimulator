using UnityEngine;

public class MedusaController : MonoBehaviour
{
    private Medusa _medusa;
    public float moveSpeedMultiplier = 2.0f;

    public void Initialize(Medusa medusa)
    {
        _medusa = medusa;
        this.moveSpeedMultiplier = medusa.moveSpeedMultiplier;
    }

    public void UpdateMovement(float dt)
    {
        // 只有在暖機完成後才執行位移
        if (!_medusa.isReady) return;

        // --- 完全還原原本的移動公式 ---
        // 計算推進速度 (Thrust)
        float thrust = 1.0f + Mathf.Sin(_medusa.phase + 4.4f) * 0.35f + _medusa.charge * 1.0f;
        float speed = thrust * dt * moveSpeedMultiplier;

        // 移動 (沿著自身的 Y 軸/頭頂方向)
        transform.position += transform.up * speed;

        // 邊界檢查 (Reset)
        if (transform.position.y > 50.0f)
        {
            _medusa.ResetPosition();
        }
    }
}