using UnityEngine;
using System.Collections; // 必須引用，用於 Coroutine

public class Medusa : MonoBehaviour
{
    [Header("Settings")]
    public int subdivisions = 40;
    public float moveSpeedMultiplier = 2.0f; // 可調整移動速度

    [Header("Initialization")]
    public int warmUpFrames = 120; // 暖機幀數 (120幀 @ 60fps ≈ 2秒)
    public bool isReady = false;   // 狀態鎖，防止在暖機完成前移動

    [Header("Simulation State")]
    public float phase = 0;
    public float charge = 0; // 互動能量
    private float time = 0;
    private float noiseSeed;

    [Header("References")]
    public MedusaBell bell;
    public MedusaTentacles tentacles;
    public MedusaOralArms oralArms;
    public Material jellyfishMaterial; 
    public Material jellyfishMaterialInside; 
    
    public ComputeShader jellyfishComputeShader;
    public VerletPhysics physics;
    public MedusaVerletBridge bridge;
    public int medusaId;

    // 將 Start 改為 IEnumerator 以支援暖機流程
    IEnumerator Start()
    {
        // 0. 鎖定狀態
        isReady = false;

        // 1. 初始化隨機數與時間
        noiseSeed = Random.Range(0f, 100f);
        time = Random.Range(0f, 5f); // 隨機初始時間

        // 2. 設定初始位置 (隨機分佈)
        // 先決定位置，避免物理 Bake 在 (0,0,0) 後被瞬間拉扯
        transform.position = new Vector3(
            (Random.value - 0.5f) * 10f,
            (Random.value - 0.5f) * 10f, 
            (Random.value - 0.5f) * 10f
        );

        // --- 【關鍵修正】預先應用旋轉 ---
        // 在建立物理頂點之前，先根據目前的 time 算出水母該有的角度。
        // 這樣生成的頂點就會直接「長在」歪斜的角度上，避免第一幀的甩動爆炸。
        ApplyRotation(0f); 

        // 3. 初始化物理系統
        physics = new VerletPhysics(jellyfishComputeShader);
        bridge = new MedusaVerletBridge(physics);
        medusaId = bridge.RegisterMedusa(this);

        // 4. 建立幾何結構
        bell = new MedusaBell(this);
        bell.CreateGeometry();
        
        tentacles = gameObject.AddComponent<MedusaTentacles>();
        tentacles.Initialize(this);
        
        // ★ 新增：建立口腕觸手 (Oral Arms) ★
        // 必須在 physics.Bake() 之前執行，因為它會註冊新的物理點和彈簧
        oralArms = gameObject.AddComponent<MedusaOralArms>();
        oralArms.Initialize(this);
        
        // 5. Bake 物理數據 
        // 此時 Transform 已經在正確的位置和旋轉角度上了，
        // Bake 出來的 Rest Position 會完美對齊。
        physics.Bake(bridge, this);
        bridge.Bake();

        // 6. 建立顯示網格
        CreateMeshObject("Bell Outside", bell.geometryOutside.mesh, jellyfishMaterial);
        CreateMeshObject("Bell Inside", bell.geometryInside.mesh, jellyfishMaterialInside);

        // --- 【暖機階段】 ---
        // 讓物理系統在原地先跑一段時間，讓觸手受重力自然垂下
        for (int i = 0; i < warmUpFrames; i++)
        {
            // 使用固定的 dt 進行模擬，保證穩定
            float warmUpDt = 0.016f; 
            
            // 更新物理 (只算軟體變形)
            if (physics != null) physics.Update(warmUpDt);

            // 更新 Shader 讓網格形狀跟隨物理運算
            UpdateShaderParams();

            yield return null; // 等待下一幀
        }

        // 7. 解鎖！開始 Update 中的移動邏輯
        isReady = true;
    }

    void Update()
    {
        // 如果還沒準備好，就跳過移動邏輯
        if (!isReady) return;

        float dt = Time.deltaTime;

        // --- 1. 更新時間與 Phase ---
        float timeStepNoise = (Mathf.PerlinNoise(noiseSeed, Time.time * 0.1f) - 0.5f) * 2.0f;
        // JS: this.time += delta * (1.0 + noise2D(...) * 0.1 + this.charge * 0.5);
        float timeStep = dt * (1.0f + timeStepNoise * 0.1f + charge * 0.5f);
        time += timeStep;

        // JS: phase = ((this.time * 0.2) % 1.0) * Math.PI * 2;
        phase = ((time * 0.2f) % 1.0f) * Mathf.PI * 2.0f;

        // --- 2. 更新位移與旋轉 ---
        ApplyRotation(dt);
        ApplyMovement(dt);

        // --- 3. 更新 Shader 參數 ---
        UpdateShaderParams();

        // --- 4. 更新物理模擬 ---
        if (physics != null)
        {
            physics.Update(dt);
        }
    }

    // 獨立出來的旋轉邏輯，方便在 Start 和 Update 共用
    void ApplyRotation(float dt)
    {
        // 模擬 Noise3D (用 Time 模擬第三維)
        float t = time * 0.1f; 

        // 計算旋轉 (Wobble)
        float rotX = (Mathf.PerlinNoise(t, noiseSeed + 13.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;
        float rotY = (Mathf.PerlinNoise(t * 0.1f, noiseSeed + 12.37f) - 0.5f) * 2.0f * Mathf.PI * 0.4f;
        float rotZ = (Mathf.PerlinNoise(t, noiseSeed + 11.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;

        // 應用旋轉
        transform.rotation = Quaternion.Euler(rotX * Mathf.Rad2Deg, rotY * Mathf.Rad2Deg, rotZ * Mathf.Rad2Deg);
    }

    // 獨立出來的移動邏輯
    void ApplyMovement(float dt)
    {
        // 計算推進速度 (Thrust)
        float thrust = 1.0f + Mathf.Sin(phase + 4.4f) * 0.35f + charge * 1.0f;
        float speed = thrust * dt * moveSpeedMultiplier;

        // 移動 (沿著自身的 Y 軸/頭頂方向)
        transform.position += transform.up * speed;

        // 邊界檢查 (Reset)
        if (transform.position.y > 50.0f)
        {
            ResetPosition();
        }
    }

    // 統一管理 Shader 參數更新
    void UpdateShaderParams()
    {
        if (physics == null || physics.vertexBuffer == null) return;

        if (jellyfishMaterial != null)
        {
            jellyfishMaterial.SetFloat("_Phase", phase);
            jellyfishMaterial.SetFloat("_Charge", charge);
            jellyfishMaterial.SetBuffer("_PhysicsBuffer", physics.vertexBuffer);
        }
        
        if (jellyfishMaterialInside != null)
        {
            jellyfishMaterialInside.SetFloat("_Phase", phase);
            jellyfishMaterialInside.SetFloat("_Charge", charge);
            jellyfishMaterialInside.SetBuffer("_PhysicsBuffer", physics.vertexBuffer);
        }
    }

    void ResetPosition()
    {
        Vector3 oldPos = transform.position;
        
        Vector3 newPos = new Vector3(
            (Random.value - 0.5f) * 10f, 
            -25f, 
            (Random.value - 0.5f) * 10f
        );

        // 1. 移動 Transform
        transform.position = newPos;
        
        // 2. 計算搬家距離
        Vector3 delta = newPos - oldPos;

        // 3. 叫物理系統一起搬家
        if (physics != null)
        {
            physics.Teleport(delta);
        }
    }

    void OnDestroy() {
        physics?.Dispose();
    }

    // Helper
    void CreateMeshObject(string name, Mesh mesh, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(this.transform, false);
        var mf = obj.AddComponent<MeshFilter>();
        var mr = obj.AddComponent<MeshRenderer>();
        mf.mesh = mesh;
        mr.material = mat;
    }
}