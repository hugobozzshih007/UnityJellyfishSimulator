// MedusaBellFormula.hlsl

#ifndef MEDUSA_BELL_FORMULA_INCLUDED
#define MEDUSA_BELL_FORMULA_INCLUDED

// --- 結構定義 ---
// ★ 修改 1: 將 VertexData 改名為 MedusaPhysicsData 以避免衝突
struct MedusaPhysicsData {
    float3 position; // 0 offset
    float isFixed;   // 24 offset
    float3 normal;   // 12 offset (新增!)
    float uvY;     // ★ 新增：0.0 = 根部, 1.0 = 尾部
};

// C# 傳入的物理數據 (World Space)
// ★ 修改 2: Buffer 宣告也要用新名稱
StructuredBuffer<MedusaPhysicsData> _PhysicsBuffer;

// --- Noise Functions (Simplex Noise 3D) ---
float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 permute(float4 x) { return mod289(((x * 34.0) + 1.0) * x); }
float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float snoise(float3 v) {
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);

    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;

    i = mod289(i);
    float4 p = permute(permute(permute(
                i.z + float4(0.0, i1.z, i2.z, 1.0))
                + i.y + float4(0.0, i1.y, i2.y, 1.0))
                + i.x + float4(0.0, i1.x, i2.x, 1.0));

    float n_ = 0.142857142857;
    float3 ns = n_ * D.wyz - D.xzx;
    float4 j = p - 49.0 * floor(p * ns.z * ns.z);

    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);

    float4 x = x_ * ns.x + ns.yyyy;
    float4 y = y_ * ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);
    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, float4(0.0, 0.0, 0.0, 0.0));

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
    float3 p0 = float3(a0.xy, h.x);
    float3 p1 = float3(a0.zw, h.y);
    float3 p2 = float3(a1.xy, h.z);
    float3 p3 = float3(a1.zw, h.w);

    float4 norm = taylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;

    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

float triNoise3D(float3 p, float speed, float time) {
    return snoise(p + float3(0, 0, time * speed));
}

// --- 主要計算函數 (Shader Graph Node) ---
void GetBellPosition_float(
    float Phase, 
    float Zenith, 
    float Azimuth, 
    float BottomFactor, 
    float4 VertexIDs, 
    float4 SideData, 
    out float3 OutPosition,
    out float3 OutNormal) // 已新增 OutNormal
{
    int vId = (int)VertexIDs.x;

    // === 情況 A: 動畫驅動 (Bell Top / Bottom) ===
    if (vId < 0) 
    {
        float sinAzimuth = sin(Azimuth);
        float cosAzimuth = cos(Azimuth);
        const float MY_PI = 3.14159265359; 

        float3 noisePos1 = float3(sinAzimuth * 0.02, cosAzimuth * 0.02, 12.69);
        float zenithNoise = triNoise3D(noisePos1, 0.2, Phase) * 6.0;
        float modifiedZenith = Zenith * (zenithNoise * 0.03 + 0.9);

        float modifiedPhase = Phase;
        float mixVal = lerp(0.0, modifiedZenith * 0.95, modifiedZenith);
        modifiedPhase -= mixVal;
        modifiedPhase += MY_PI * 0.5;

        float xr = sin(modifiedPhase) * 0.3 + 1.3;
        float smoothZ = smoothstep(0.5, 1.0, Zenith);
        float riffleWave = sin(Azimuth * 16.0 + 0.5 * MY_PI) * 0.02 + 1.0;
        float riffles = lerp(1.0, riffleWave, smoothZ);
        xr *= riffles;

        float polarAngle = (sin(modifiedPhase + 3.0) * 0.15 + 0.5) * modifiedZenith * MY_PI;
        float3 result = float3(0, 0, 0);
        float radius = sin(polarAngle) * xr;
        
        result.y = cos(polarAngle);
        result.z = cosAzimuth * radius;
        result.x = sinAzimuth * radius;

        float3 noisePos2 = float3(sinAzimuth * modifiedZenith * 0.02, cosAzimuth * modifiedZenith * 0.02, 42.69);
        float bumpNoise = triNoise3D(noisePos2, 0.2, Phase) * 6.0;
        result += bumpNoise * 0.02;

        float flattenFactor = smoothstep(0.0, 0.95, 1.0 - Zenith) * 0.1 * BottomFactor;
        result.y = lerp(result.y, 0.0, flattenFactor);

        OutPosition = result;
        OutNormal = float3(0, 1, 0); // Placeholder for procedural part
    } 
    // === 情況 B: 物理驅動 (Bell Margin / Skirt) ===
    else 
    {
        #ifdef IS_COMPUTE_SHADER
            OutPosition = float3(0, 0, 0);
            OutNormal = float3(0, 1, 0);
        #else
            int id0 = (int)VertexIDs.x;
            int id1 = (int)VertexIDs.y;
            int id2 = (int)VertexIDs.z;
            int id3 = (int)VertexIDs.w;

            // ★ 修改 3: 這裡讀取也要用新結構名稱 (雖然這裡是 _PhysicsBuffer[...]，編譯器會自動推導)
            float3 p0 = _PhysicsBuffer[id0].position;
            float3 p1 = _PhysicsBuffer[id1].position;
            float3 p2 = _PhysicsBuffer[id2].position;
            float3 p3 = _PhysicsBuffer[id3].position;

            p0 = mul(unity_WorldToObject, float4(p0, 1.0)).xyz;
            p1 = mul(unity_WorldToObject, float4(p1, 1.0)).xyz;
            p2 = mul(unity_WorldToObject, float4(p2, 1.0)).xyz;
            p3 = mul(unity_WorldToObject, float4(p3, 1.0)).xyz;

            float3 top = (p0 + p1) * 0.5;
            float3 bottom = (p2 + p3) * 0.5;
            float3 left = (p0 + p2) * 0.5;
            float3 right = (p1 + p3) * 0.5;

            float3 pos = (top + bottom) * 0.5; 
            float3 tangent = bottom - top;
            float3 bitangent = right - left;

            float3 normalDir = normalize(cross(tangent, bitangent));
            normalDir *= SideData.z;
            normalDir += normalize(tangent) * SideData.y;
            
            float width = SideData.w;
            OutPosition = pos + normalDir * width;
            OutNormal = normalize(normalDir);
        #endif
    }
}

#endif