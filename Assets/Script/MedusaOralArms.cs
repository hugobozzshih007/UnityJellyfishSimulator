using UnityEngine;
using System.Collections.Generic;

public class MedusaOralArms : MonoBehaviour
{
    [Header("Randomness")]
    public int randomSeed = 0;
    
    [Header("Physics Settings")]
    [Range(0.1f, 1.0f)]
    public float stretchStiffness = 0.1f; 

    public float baseBendStiffness = 0.1f; 
    public float tipBendStiffness = 0.005f;
    
    [Header("Ruffle Growth")]
    [Range(0f, 1f)] public float ruffleStartPct = 0.0f;
    [Range(0f, 1f)] public float ruffleEndPct = 0.95f;
    
    [Header("Shape Profiling")]
    [Range(0.1f, 0.9f)] public float rufflePeakPos = 0.25f;
    [Range(0f, 1f)] public float rootRuffleScale = 0.01f; 
    
    [Header("Tube Swelling (New)")]
    public float swellStart = 0.1f;    // 開始變粗
    public float swellPeak = 0.25f;     // 最粗位置
    public float swellAmount = 1.5f;   // 膨脹倍率 (2倍)
    public float tubeThinStart = 0.4f; // 開始變極細的位置 (需大於 swellPeak)
    public float tubeThinScale = 0.05f;

    [Header("Twist Settings")]
    public float totalTwist = 360.0f;
    
    private Medusa medusa;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    [Header("Structure Settings")]
    public float armLength = 8.0f; 
    public int physicsNodes = 50; 
    [Range(0f, 1f)] public float attachmentZenith = 0.2f; 
    public float rootDistributionRadius = 0.4f;

    [Header("Tube Appearance")]
    public float tubeThickness = 0.1f; 

    [Header("Mesh Density")]
    public int visualSegmentsPerNode = 6; 
    
    const int armsNum = 4;        
    const int finsPerArm = 6;     

    [Header("Ruffle Shape")]
    public float ruffleWidth = 2.2f;  
    public float ruffleFoldAmt = 2.5f;  
    public float noiseFreq = .65f;      

    public void Initialize(Medusa medusa, Material material)
    {
        this.medusa = medusa;
        CreateGeometry(material);
    }

    void CreateGeometry(Material material = null)
    {
        VerletPhysics physics = medusa.physics;
        MedusaVerletBridge bridge = medusa.bridge;
        
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>(); 
        List<Vector2> uvs0 = new List<Vector2>(); 
        List<Vector2> uvs1 = new List<Vector2>(); 
        List<Vector2> uvs2 = new List<Vector2>(); 
        List<int> triangles = new List<int>();

        Random.InitState(randomSeed);
        List<List<int>> allArmsPhysicsIds = new List<List<int>>();

        // =================================================================================
        // PHASE 1: PHYSICS GENERATION (保持原始單鏈)
        // =================================================================================
        for (int i = 0; i < armsNum; i++)
        {
            float lengthScale = Random.Range(0.75f, 1.5f);
            float currentArmLength = armLength * lengthScale;
            float currentNodeSpacing = currentArmLength / (float)physicsNodes;

            List<int> currentArmIds = new List<int>();
            float azimuth = (i / (float)armsNum) * Mathf.PI * 2.0f;
            Vector3 currentOffset = Vector3.zero;

            for (int p = 0; p < physicsNodes; p++)
            {
                bool isFixed = (p == 0);
                float t = p / (float)physicsNodes; 
                int id = physics.AddVertex(medusa.transform.position, isFixed, t);
                currentArmIds.Add(id);

                bridge.RegisterVertex(medusa.medusaId, id, attachmentZenith, azimuth, true, currentOffset, 0f, isFixed);

                float bendingStiffness = Mathf.Lerp(tipBendStiffness, baseBendStiffness, 1.0f - t);
                if (p > 0) physics.AddSpring(id, currentArmIds[p-1], stretchStiffness, 1.0f);
                if (p > 1) physics.AddSpring(id, currentArmIds[p-2], bendingStiffness, 1.0f);

                currentOffset.y -= currentNodeSpacing;
            }
            allArmsPhysicsIds.Add(currentArmIds);
        }

        // =================================================================================
        // PHASE 2: VISUAL MESH GENERATION (形狀 logic 修正)
        // =================================================================================
        Random.InitState(randomSeed);

        for (int i = 0; i < armsNum; i++)
        {
            float lengthScale = Random.Range(0.9f, 1.2f);
            float currentArmLength = armLength * lengthScale;
            float currentNodeSpacing = currentArmLength / (float)physicsNodes;
            float armSeed = i * 13.5f + randomSeed * 7.1f;
            float twistScale = Random.Range(0.8f, 1.2f); 

            List<int> boneIds = allArmsPhysicsIds[i];
            int totalVisualLayers = (physicsNodes - 1) * visualSegmentsPerNode + 1;
            float[] ruffleDistAccumulator = new float[finsPerArm]; 
            Vector3[] prevRufflePos = new Vector3[finsPerArm];

            for (int v = 0; v < totalVisualLayers; v++)
            {
                float progress = v / (float)(totalVisualLayers - 1); 

                float t = v / (float)visualSegmentsPerNode;
                int pIndex = Mathf.FloorToInt(t);
                float lerpT = t - pIndex;
                if (pIndex >= physicsNodes - 1) { pIndex = physicsNodes - 2; lerpT = 1.0f; }
                
                Vector2 bindingData = new Vector2(boneIds[pIndex], lerpT);
                float currentY = -v * (currentNodeSpacing / visualSegmentsPerNode);
                Vector3 centerPos = new Vector3(0, currentY, 0);

                // --- 【新 Tube Scale 曲線邏輯】 ---
                float tubeScale = 1.0f;
                if (progress <= swellPeak)
                {
                    // 從 swellStart 到 swellPeak 漸漸變粗到 swellAmount (2倍)
                    float t_swell = Mathf.InverseLerp(swellStart, swellPeak, progress);
                    tubeScale = Mathf.Lerp(1.0f, swellAmount, Mathf.SmoothStep(0, 1, t_swell));
                }
                else
                {
                    // 從 swellPeak 開始，向 tubeThinScale 進行縮減
                    float t_shrink = Mathf.InverseLerp(swellPeak, tubeThinStart, progress);
                    tubeScale = Mathf.Lerp(swellAmount, tubeThinScale, Mathf.SmoothStep(0, 1, t_shrink));
                }

                // Ruffle Profile
                float widthProfile = (progress < rufflePeakPos) ? Mathf.SmoothStep(rootRuffleScale, 1f, progress / rufflePeakPos) : Mathf.Lerp(1.0f, 0.2f, Mathf.Pow((progress - rufflePeakPos) / (1.0f - rufflePeakPos), 3.0f));
                float ruffleIntensity = widthProfile * (1.0f - Mathf.SmoothStep(ruffleEndPct - 0.1f, ruffleEndPct, progress));

                float currentTwistRad = progress * totalTwist * twistScale * Mathf.Deg2Rad;
                float gapMultiplier = Mathf.Max(0.5f + Mathf.Sin(v * noiseFreq * 0.5f + armSeed) * 0.5f + (Mathf.PerlinNoise(armSeed, v * noiseFreq * 0.2f) - 0.5f) * 2.0f, 0.6f); 

                for (int f = 0; f < finsPerArm; f++)
                {
                    float baseAngle = (f / (float)finsPerArm) * Mathf.PI * 2.0f;
                    float twistedAngle = baseAngle + currentTwistRad;
                    Vector3 dir = new Vector3(Mathf.Cos(twistedAngle), 0, Mathf.Sin(twistedAngle));

                    // Tube
                    float currentTubeR = tubeThickness * tubeScale;
                    vertices.Add(centerPos + dir * currentTubeR);
                    normals.Add(dir);
                    uvs0.Add(new Vector2(0, progress)); uvs1.Add(bindingData); uvs2.Add(new Vector2(0, -currentY)); 

                    // Ruffle
                    float expansion = ruffleWidth * ruffleIntensity * gapMultiplier * (1.0f + (Mathf.PerlinNoise(armSeed + f * 10, v * noiseFreq) - 0.5f));
                    Vector3 ruffleDir = dir;
                    floatRxz(ref ruffleDir, Mathf.Cos(Mathf.Sin(v * noiseFreq * 0.5f + f) * 0.2f), Mathf.Sin(Mathf.Sin(v * noiseFreq * 0.5f + f) * 0.2f));

                    Vector3 rufflePos = centerPos + ruffleDir * (currentTubeR + expansion);
                    vertices.Add(rufflePos);
                    normals.Add(Vector3.Slerp(dir, Vector3.up, 0.15f).normalized);
                    
                    if (v > 0) ruffleDistAccumulator[f] += Vector3.Distance(prevRufflePos[f], rufflePos);
                    prevRufflePos[f] = rufflePos;

                    uvs0.Add(new Vector2(1, progress)); uvs1.Add(bindingData); uvs2.Add(new Vector2(1, ruffleDistAccumulator[f]));

                    // Faces
                    if (v > 0)
                    {
                        int vertsPerLayer = finsPerArm * 2;
                        int currentBase = vertices.Count - 2;
                        int prevBase = currentBase - vertsPerLayer;
                        int nextOffset = (f == finsPerArm - 1) ? -(finsPerArm - 1) * 2 : 2;

                        triangles.Add(prevBase); triangles.Add(prevBase + nextOffset); triangles.Add(currentBase);
                        triangles.Add(prevBase + nextOffset); triangles.Add(currentBase + nextOffset); triangles.Add(currentBase);
                        triangles.Add(prevBase); triangles.Add(prevBase + 1); triangles.Add(currentBase);
                        triangles.Add(prevBase + 1); triangles.Add(currentBase + 1); triangles.Add(currentBase);
                        triangles.Add(prevBase); triangles.Add(currentBase); triangles.Add(prevBase + 1);
                        triangles.Add(prevBase + 1); triangles.Add(currentBase); triangles.Add(currentBase + 1);
                    }
                }
            }
        }
        
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.SetUVs(0, uvs0); mesh.SetUVs(1, uvs1); mesh.SetUVs(2, uvs2); 
        mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
        mesh.RecalculateTangents();

        if (meshRenderer == null) {
            GameObject obj = new GameObject("OralArms_Skinned");
            obj.transform.SetParent(medusa.transform, false);
            meshRenderer = obj.AddComponent<MeshRenderer>();
            obj.AddComponent<MeshFilter>().mesh = mesh;
        }

        if (material == null)
        {
            Shader shader = Shader.Find("Shader Graphs/OralArms");
            if (shader != null) 
                meshRenderer.material = new Material(shader) { renderQueue = 2950 };
        }
        else
        {
            meshRenderer.material = material;
        }
        
        propBlock = new MaterialPropertyBlock();
    }

    void floatRxz(ref Vector3 v, float cosA, float sinA) {
        float x = v.x * cosA - v.z * sinA;
        float z = v.x * sinA + v.z * cosA;
        v.x = x; v.z = z;
    }
    
    void Update()
    {
        if (meshRenderer != null && medusa.physics != null && medusa.physics.vertexBuffer != null)
        {
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetBuffer("_PhysicsBuffer", medusa.physics.vertexBuffer);
            propBlock.SetFloat("_Charge", medusa.charge);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}