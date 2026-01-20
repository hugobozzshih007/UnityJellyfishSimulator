using UnityEngine;
using Unity.Mathematics;

public static class MedusaBellFormula
{
    // 模擬 Three.js 的 triNoise3D (實際上是 Simplex Noise 的變體)
    // 這裡我們直接使用 Unity.Mathematics.noise.snoise (Simplex Noise)
    // 注意：原始碼中的 triNoise3D 可能有特定的參數簽名，這裡我們適配它
    private static float TriNoise3D(float3 position, float speed, float time)
    {
        // 原始 JS: triNoise3D(vec3, speed, time)
        // 這裡我們將 time * speed 加入到 noise 的第 4 維度或偏移量中來模擬動態
        // 為了簡化，我們用 snoise(float3) 並讓 time 影響 position
        float3 offset = new float3(0, 0, time * speed); 
        return noise.snoise(position + offset);
    }

    /// <summary>
    /// 計算水母本體頂點位置
    /// </summary>
    /// <param name="phase">動畫相位 (時間 * 速度)</param>
    /// <param name="zenith">垂直角度參數 (0~1)</param>
    /// <param name="azimuth">水平角度參數 (0~2PI)</param>
    /// <param name="bottomFactor">底部平坦化因子 (外殼=0, 內殼=1)</param>
    /// <returns>模型空間中的頂點位置</returns>
    public static float3 GetBellPosition(float phase, float zenith, float azimuth, float bottomFactor = 0)
    {
        // 1. 基礎三角函數
        float sinAzimuth = math.sin(azimuth);
        float cosAzimuth = math.cos(azimuth);

        // 2. 垂直方向雜訊 (Zenith Noise)
        // JS: vec3(sinAzimuth * 0.02, cosAzimuth * 0.02, 12.69)
        float3 noisePos1 = new float3(sinAzimuth * 0.02f, cosAzimuth * 0.02f, 12.69f);
        // JS: triNoise3D(..., 0.2, time) * 6.0
        // 這裡 phase 通常已經包含了 time，或者我們需要額外傳入 time。
        // 假設 phase 本身就是驅動動畫的主變量，我們用 phase 代替 time 進行雜訊滾動
        float zenithNoise = TriNoise3D(noisePos1, 0.2f, phase) * 6.0f;

        // JS: zenith * (zenithNoise * 0.03 + 0.9)
        float modifiedZenith = zenith * (zenithNoise * 0.03f + 0.9f);

        // 3. 相位與波動 (Phase & Wave)
        float modifiedPhase = phase;
        // JS: sub(mix(0.0, modifiedZenith * 0.95, modifiedZenith))
        // mix(a, b, t) = a + (b - a) * t
        float mixVal = math.lerp(0.0f, modifiedZenith * 0.95f, modifiedZenith);
        modifiedPhase -= mixVal;
        modifiedPhase += math.PI * 0.5f;

        // JS: sin(modifiedPhase) * 0.3 + 1.3
        float xr = math.sin(modifiedPhase) * 0.3f + 1.3f;

        // 4. 表面皺褶 (Riffles)
        // JS: smoothstep(0.5, 1.0, zenith)
        float smoothZ = math.smoothstep(0.5f, 1.0f, zenith);
        // JS: sin(azimuth * 16.0 + 0.5*PI) * 0.02 + 1.0
        float riffleWave = math.sin(azimuth * 16.0f + 0.5f * math.PI) * 0.02f + 1.0f;
        // JS: mix(1.0, ..., smoothZ)
        float riffles = math.lerp(1.0f, riffleWave, smoothZ);
        
        xr *= riffles;

        // 5. 極座標轉笛卡爾座標
        // JS: (sin(modifiedPhase + 3.0) * 0.15 + 0.5) * modifiedZenith * PI
        float polarAngle = (math.sin(modifiedPhase + 3.0f) * 0.15f + 0.5f) * modifiedZenith * math.PI;

        float3 result = float3.zero;
        
        // 暫存半徑
        float radius = math.sin(polarAngle) * xr;
        
        // 計算高度 Y
        result.y = math.cos(polarAngle); // yr = 1.0

        // 計算 X, Z
        result.z = cosAzimuth * radius;
        result.x = sinAzimuth * radius;

        // 6. 表面凹凸細節 (Bump Noise)
        // JS: vec3(sinAzimuth * modifiedZenith * 0.02, cosAzimuth * modifiedZenith * 0.02, 42.69)
        float3 noisePos2 = new float3(sinAzimuth * modifiedZenith * 0.02f, cosAzimuth * modifiedZenith * 0.02f, 42.69f);
        float bumpNoise = TriNoise3D(noisePos2, 0.2f, phase) * 6.0f;
        
        result += bumpNoise * 0.02f;

        // 7. 底部平坦化 (Flattening)
        // JS: smoothstep(0, 0.95, 1.0 - zenith) * 0.1 * bottomFactor
        float flattenFactor = math.smoothstep(0.0f, 0.95f, 1.0f - zenith) * 0.1f * bottomFactor;
        result.y = math.lerp(result.y, 0.0f, flattenFactor);

        return result;
    }
}