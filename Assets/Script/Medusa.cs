using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Medusa : MonoBehaviour
{
    [Header("References")]
    public MedusaSpeciesConfig config;
    public ComputeShader jellyfishComputeShader;
    public VerletPhysics physics;
    public MedusaVerletBridge bridge;
    public MedusaController controller;
    
    [Header("具名組件插槽")]
    public MedusaBellTopBase bellTop;
    public MedusaBellBottomBase bellBottom;
    public MedusaBellMarginBase bellMargin;
    public MedusaOralArmsBase oralArms;
    public MedusaTentaclesBase tentacles;
    
    [Header("Runtime State")]
    public bool isReady = false;
    public float phase = 0;
    public float charge = 0;
    private float time = 0;
    
    // 共享的幾何容器 (用於 Bell Top/Bottom/Margin)
    public MedusaBellGeometry geometryOutside { get; private set; }
    public MedusaBellGeometry geometryInside { get; private set; }
    
    [Header("Initialization")]
    public int warmUpFrames = 120;
    private float noiseSeed;
    
    // 確保保留 medusaId 變數
    public int medusaId;

    IEnumerator Start()
    {
        isReady = false;
        noiseSeed = Random.Range(0f, 100f);
        time = Random.Range(0f, 5f);

        // 初始化移動控制器
        controller = gameObject.AddComponent<MedusaController>();
        controller.Initialize(this);
        
        physics = new VerletPhysics(jellyfishComputeShader);
        bridge = new MedusaVerletBridge(physics);

        transform.position = new Vector3(
            (Random.value - 0.5f) * 10f,
            (Random.value - 0.5f) * 10f, 
            (Random.value - 0.5f) * 10f
        );

        // 2. 建立共享幾何緩衝
        geometryOutside = new MedusaBellGeometry(this, true);
        geometryInside = new MedusaBellGeometry(this, false);
        
        // 3. 依序組裝部件 (順序很重要，因為有依賴關係)
        bellTop = SpawnModule(config.bellTopPrefab);
        bellBottom = SpawnModule(config.bellBottomPrefab);
        bellMargin = SpawnModule(config.bellMarginPrefab); // 依賴 Top
        oralArms = SpawnModule(config.oralArmsPrefab);
        tentacles = SpawnModule(config.tentaclesPrefab); // 依賴 Margin
        
        // 註冊 Medusa 並取得 medusaId
        medusaId = bridge.RegisterMedusa(this);

        //bell = new MedusaBell(this);
        //bell.CreateGeometry();
        //tentacles = gameObject.AddComponent<MedusaTentacles>();
        //tentacles.Initialize(this);
        //oralArms = gameObject.AddComponent<MedusaOralArms>();
        //oralArms.Initialize(this, config.oralArmsMaterial);
        
        physics.Bake(bridge, this);
        bridge.Bake();
        geometryOutside.BakeGeometry();
        geometryInside.BakeGeometry();

        CreateMeshObject("Bell Outside", geometryOutside.mesh, config.jellyfishMaterialOutside);
        CreateMeshObject("Bell Inside", geometryInside.mesh, config.jellyfishMaterialInside);

        
        // 初始物理應用前先應用旋轉
        ApplyRotation(0f);
        // 暖機階段
        for (int i = 0; i < warmUpFrames; i++)
        {
            if (physics != null) physics.Update(0.016f);
            UpdateShaderParams();
            yield return null;
        }
        isReady = true;
    }

    private T SpawnModule<T>(T prefab) where T : MedusaModule
    {
        if (prefab == null) return null;
        T instance = Instantiate(prefab, transform);
        instance.Initialize(this);
        return instance;
    }
    
    void Update()
    {
        if (!isReady) return;
        float dt = Time.deltaTime;

        // 1. 取得瞬時轉向強度
        float instantTurn = (controller != null) ? controller.GetTurnFactor() : 0f;

        // 2. ★ 核心修正：將瞬時值平滑化 ★
        // 如果 instantTurn > 0，代表正在轉，目標倍率提高
        // 使用 Lerp 讓 currentFreqMultiplier 緩緩爬升或下降
        float targetMultiplier = 1.0f + (instantTurn * config.frequencyBoost);
        float currentFreqMultiplier = Mathf.Lerp(config.currentFreqMultiplier, targetMultiplier, dt * config.freqLerpSpeed);

        // 3. 更新時間與相位 (使用平滑後的倍率)
        float timeStepNoise = (Mathf.PerlinNoise(noiseSeed, Time.time * 0.1f) - 0.5f) * 2.0f;
    
        // phase 的增長速度現在由 currentFreqMultiplier 決定 
        time += dt * currentFreqMultiplier * (1.0f + timeStepNoise * 0.1f + charge * 0.5f);
        phase = ((time * 0.2f) % 1.0f) * Mathf.PI * 2.0f;

        if (controller != null) controller.UpdateMovement(dt);
        UpdateShaderParams();
        // 由主體統一分配時間步長 (dt) 給各個部件
        if (bellTop != null)    bellTop.UpdateModule(dt);
        if (bellBottom != null) bellBottom.UpdateModule(dt);
        if (bellMargin != null) bellMargin.UpdateModule(dt);
        if (oralArms != null)   oralArms.UpdateModule(dt);
        if (tentacles != null)  tentacles.UpdateModule(dt);
        if (physics != null) physics.Update(dt);
    }

    // 隨機晃動逻辑 (僅在不主動追蹤時備用，目前 Update 已停用)
    void ApplyRotation(float dt)
    {
        float t = time * 0.1f; 
        float rotX = (Mathf.PerlinNoise(t, noiseSeed + 13.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;
        float rotY = (Mathf.PerlinNoise(t * 0.1f, noiseSeed + 12.37f) - 0.5f) * 2.0f * Mathf.PI * 0.4f;
        float rotZ = (Mathf.PerlinNoise(t, noiseSeed + 11.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;
        transform.rotation = Quaternion.Euler(rotX * Mathf.Rad2Deg, rotY * Mathf.Rad2Deg, rotZ * Mathf.Rad2Deg);
    }

    public void ResetPosition()
    {
        Vector3 oldPos = transform.position;
        Vector3 newPos = new Vector3((Random.value - 0.5f) * 10f, -25f, (Random.value - 0.5f) * 10f);
        transform.position = newPos;
        Vector3 delta = newPos - oldPos;
        if (physics != null) physics.Teleport(delta);
    }

    void UpdateShaderParams()
    {
        if (physics == null || physics.vertexBuffer == null) return;
        // [新增] 計算擺動振幅縮減：當頻率加成越高，振幅越小
        // 取得目前轉向因子
        //float turnFactor = (controller != null) ? controller.GetTurnFactor() : 0f;
        // 1.0 是正常張開，0.5 代表張開角度減半 (數值可依手感調整)
        //float amplitudeScale = Mathf.Lerp(1.0f, 0.6f, turnFactor);
        Material[] mats = { config.jellyfishMaterialOutside, config.jellyfishMaterialInside };
        foreach (var mat in mats) {
            if (mat != null) {
                mat.SetFloat("_Phase", phase);
                mat.SetFloat("_Charge", charge);
                mat.SetBuffer("_PhysicsBuffer", physics.vertexBuffer);
            }
        }
    }

    void CreateMeshObject(string name, Mesh mesh, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(this.transform, false);
        obj.AddComponent<MeshFilter>().mesh = mesh;
        obj.AddComponent<MeshRenderer>().material = mat;
    }
    
    void OnDestroy() { physics?.Dispose(); }
}