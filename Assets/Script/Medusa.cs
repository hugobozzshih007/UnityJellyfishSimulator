using UnityEngine;
using System.Collections;

public class Medusa : MonoBehaviour
{
    [Header("Settings")]
    public int subdivisions = 40;
    public float moveSpeedMultiplier = 2.0f;

    [Header("Initialization")]
    public int warmUpFrames = 120; 
    public bool isReady = false;   

    [Header("Simulation State")]
    public float phase = 0;
    public float charge = 0; 
    private float time = 0;
    private float noiseSeed;

    [Header("References")]
    public MedusaController controller; // 新增控制器引用
    public MedusaBell bell;
    public MedusaTentacles tentacles;
    public MedusaOralArms oralArms;
    public Material jellyfishMaterial; 
    public Material jellyfishMaterialInside; 
    public ComputeShader jellyfishComputeShader;
    public VerletPhysics physics;
    public MedusaVerletBridge bridge;
    public int medusaId;

    IEnumerator Start()
    {
        isReady = false;

        // 1. 初始化隨機數與控制器
        noiseSeed = Random.Range(0f, 100f);
        time = Random.Range(0f, 5f);
        
        controller = gameObject.AddComponent<MedusaController>();
        controller.moveSpeedMultiplier = this.moveSpeedMultiplier;
        controller.Initialize(this);

        // 2. 設定初始隨機位置
        transform.position = new Vector3(
            (Random.value - 0.5f) * 10f,
            (Random.value - 0.5f) * 10f, 
            (Random.value - 0.5f) * 10f
        );

        // 預先應用旋轉避免物理頂點爆炸
        ApplyRotation(0f); 

        // 3. 初始化物理與幾何結構
        physics = new VerletPhysics(jellyfishComputeShader);
        bridge = new MedusaVerletBridge(physics);
        medusaId = bridge.RegisterMedusa(this);

        bell = new MedusaBell(this);
        bell.CreateGeometry();
        
        tentacles = gameObject.AddComponent<MedusaTentacles>();
        tentacles.Initialize(this);
        
        oralArms = gameObject.AddComponent<MedusaOralArms>();
        oralArms.Initialize(this);
        
        physics.Bake(bridge, this);
        bridge.Bake();

        // 4. 建立顯示網格
        CreateMeshObject("Bell Outside", bell.geometryOutside.mesh, jellyfishMaterial);
        CreateMeshObject("Bell Inside", bell.geometryInside.mesh, jellyfishMaterialInside);

        // 5. 【暖機階段】讓觸手受重力自然垂下
        for (int i = 0; i < warmUpFrames; i++)
        {
            float warmUpDt = 0.016f; 
            if (physics != null) physics.Update(warmUpDt);
            UpdateShaderParams();
            yield return null; 
        }

        isReady = true;
    }

    void Update()
    {
        if (!isReady) return;

        float dt = Time.deltaTime;

        // --- 1. 更新時間與相位 (影響 Shader 與 移動節奏) ---
        float timeStepNoise = (Mathf.PerlinNoise(noiseSeed, Time.time * 0.1f) - 0.5f) * 2.0f;
        float timeStep = dt * (1.0f + timeStepNoise * 0.1f + charge * 0.5f);
        time += timeStep;
        phase = ((time * 0.2f) % 1.0f) * Mathf.PI * 2.0f;

        // --- 2. 分工處理：旋轉歸 Medusa，位移歸 Controller ---
        ApplyRotation(dt); 
        if (controller != null)
        {
            controller.UpdateMovement(dt);
        }

        // --- 3. 更新 Shader 與 GPU 物理模擬 ---
        UpdateShaderParams();
        if (physics != null)
        {
            physics.Update(dt);
        }
    }

    // 旋轉邏輯：目前保留隨機晃動，晚上可重構成指向性旋轉
    void ApplyRotation(float dt)
    {
        float t = time * 0.1f; 
        float rotX = (Mathf.PerlinNoise(t, noiseSeed + 13.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;
        float rotY = (Mathf.PerlinNoise(t * 0.1f, noiseSeed + 12.37f) - 0.5f) * 2.0f * Mathf.PI * 0.4f;
        float rotZ = (Mathf.PerlinNoise(t, noiseSeed + 11.37f) - 0.5f) * 2.0f * Mathf.PI * 0.2f;

        transform.rotation = Quaternion.Euler(rotX * Mathf.Rad2Deg, rotY * Mathf.Rad2Deg, rotZ * Mathf.Rad2Deg);
    }

    public void ResetPosition() // 改為 public 讓 Controller 呼叫
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
        Material[] mats = { jellyfishMaterial, jellyfishMaterialInside };
        foreach (var mat in mats)
        {
            if (mat != null)
            {
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