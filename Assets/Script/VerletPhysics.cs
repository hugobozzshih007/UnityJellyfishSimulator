using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct VertexData
{
    public Vector3 position;
    public float isFixed; // 1.0 = Fixed, 0.0 = Free
    public Vector3 normal; 
    public float uvY;
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct SpringData
{
    public int indexA;
    public int indexB;
    public float stiffness;
    public float restLength; 
}

public class VerletPhysics
{
    // ★★★ 1. 新增：全域水流參數 ★★★
    [Header("Global Water Physics")]
    public float waterDrag = 0.998f;
    public Vector3 waterCurrent = new Vector3(0.5f, 0f, 0.2f); // 恆定水流方向
    public float turbulenceStrength = 1f; // 亂流強度 (建議從小開始調)
    public float turbulenceFreq = 1f;     // 亂流頻率 (空間密度)
    public float turbulenceSpeed = 0.8f;    // 亂流速度 (時間變化)

    public List<VertexData> vertices = new List<VertexData>();
    public List<SpringData> springs = new List<SpringData>();
    
    private List<List<int>> vertexSpringConnections = new List<List<int>>();
    private List<List<int>> vertexSpringSigns = new List<List<int>>();

    private ComputeShader _computeShader;
    
    private int _kUpdatePos;
    private int _kInitSprings;
    private int _kCalculateForces;
    private int _kApplyMovements;
    private int _kUpdateOralArms;
    
    public ComputeBuffer vertexBuffer;
    private ComputeBuffer springBuffer;
    private ComputeBuffer forceBuffer;
    private ComputeBuffer bridgeBuffer;
    private ComputeBuffer influencerPtrBuffer;
    private ComputeBuffer influencerBuffer;
    
    private VertexData[] _gpuDownloadCache;
    
    private bool _isBaked = false;
    private Medusa _medusaRef;

    private float _timeAccumulator = 0f;
    private const float FIXED_TIME_STEP = 1.0f / 360.0f; 

    private Vector3 _prevPosition;
    private Quaternion _prevRotation;
    private bool _firstFrame = true;

    public VerletPhysics(ComputeShader shader)
    {
        _computeShader = shader;
    }
    
    public void PullFromGPU()
    {
        if (vertexBuffer == null) return;
        if (_gpuDownloadCache == null || _gpuDownloadCache.Length != vertexBuffer.count)
            _gpuDownloadCache = new VertexData[vertexBuffer.count];
        vertexBuffer.GetData(_gpuDownloadCache);
    }
    
    public void PushToGPU()
    {
        if (vertexBuffer == null || _gpuDownloadCache == null) return;
        vertexBuffer.SetData(_gpuDownloadCache);
    }
    
    public Vector3 GetPosition(int id)
    {
        if (_gpuDownloadCache == null || id < 0 || id >= _gpuDownloadCache.Length) return Vector3.zero;
        return _gpuDownloadCache[id].position;
    }

    public void SetPosition(int id, Vector3 newPos)
    {
        if (_gpuDownloadCache == null || id < 0 || id >= _gpuDownloadCache.Length) return;
        _gpuDownloadCache[id].position = newPos;
    }

    public int AddVertex(Vector3 position, bool isFixed, float progress = 0.0f)
    {
        int id = vertices.Count;
        float fixedVal = isFixed ? 1.0f : 0.0f; 
        vertices.Add(new VertexData { 
            position = position, 
            isFixed = fixedVal, 
            normal = Vector3.up, 
            uvY = progress // ★ 存入進度
        });
        vertexSpringConnections.Add(new List<int>());
        vertexSpringSigns.Add(new List<int>());
        return id;
    }

    public int AddSpring(int v0Id, int v1Id, float stiffness, float restLengthFactor = 1.0f)
    {
        int id = springs.Count;
        springs.Add(new SpringData
        {
            indexA = v0Id, indexB = v1Id, stiffness = stiffness, restLength = restLengthFactor 
        });
        vertexSpringConnections[v0Id].Add(id);
        vertexSpringSigns[v0Id].Add(1);
        vertexSpringConnections[v1Id].Add(id);
        vertexSpringSigns[v1Id].Add(-1);
        return id;
    }
    
    public int GetNodeCount()
    {
        return vertices.Count;
    }

    public void Bake(MedusaVerletBridge bridge, Medusa medusa)
    {
        if (_isBaked || _computeShader == null) return;
        _medusaRef = medusa;

        bridge.Bake();

        if (vertices.Count > 0)
        {
            vertexBuffer = new ComputeBuffer(vertices.Count, Marshal.SizeOf(typeof(VertexData)));
            vertexBuffer.SetData(vertices.ToArray());
            forceBuffer = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
            forceBuffer.SetData(new Vector3[vertices.Count]);
        }

        if (springs.Count > 0)
        {
            springBuffer = new ComputeBuffer(springs.Count, Marshal.SizeOf(typeof(SpringData)));
            springBuffer.SetData(springs.ToArray());
        }

        List<Vector2Int> ptrs = new List<Vector2Int>();
        List<int> influencers = new List<int>();
        foreach (var conns in vertexSpringConnections)
        {
            int start = influencers.Count;
            int count = conns.Count;
            ptrs.Add(new Vector2Int(start, count));
            int vIdx = ptrs.Count - 1;
            var signs = vertexSpringSigns[vIdx];
            for(int k=0; k<count; k++) influencers.Add((conns[k] + 1) * signs[k]);
        }
        
        influencerPtrBuffer = new ComputeBuffer(ptrs.Count, sizeof(int) * 2);
        influencerPtrBuffer.SetData(ptrs.ToArray());
        if (influencers.Count > 0) {
            influencerBuffer = new ComputeBuffer(influencers.Count, sizeof(int));
            influencerBuffer.SetData(influencers.ToArray());
        }
        
        if (bridge.bridgeVertices.Count > 0) {
            bridgeBuffer = new ComputeBuffer(bridge.bridgeVertices.Count, Marshal.SizeOf(typeof(MedusaVerletBridge.BridgeData)));
            bridgeBuffer.SetData(bridge.bridgeVertices.ToArray());
        }

        _kUpdatePos = _computeShader.FindKernel("CSMain_UpdatePositions");
        _kInitSprings = _computeShader.FindKernel("CSMain_InitSprings");
        _kCalculateForces = _computeShader.FindKernel("CSMain_CalculateForces");
        _kApplyMovements = _computeShader.FindKernel("CSMain_ApplyMovements");
        _kUpdateOralArms = _computeShader.FindKernel("CSMain_UpdateOralArms");

        _computeShader.SetInt("_VertexCount", vertices.Count);
        _computeShader.SetInt("_SpringCount", springs.Count);
        if (bridgeBuffer != null) _computeShader.SetInt("_BridgeCount", bridgeBuffer.count);

        int[] kernels = { _kUpdatePos, _kInitSprings, _kCalculateForces, _kApplyMovements, _kUpdateOralArms };
        foreach (var k in kernels)
        {
            if(vertexBuffer != null) _computeShader.SetBuffer(k, "_VertexData", vertexBuffer);
            if(springBuffer != null) _computeShader.SetBuffer(k, "_SpringData", springBuffer);
            if(forceBuffer != null) _computeShader.SetBuffer(k, "_ForceData", forceBuffer);
            if(bridgeBuffer != null) _computeShader.SetBuffer(k, "_BridgeData", bridgeBuffer);
            if(influencerPtrBuffer != null) _computeShader.SetBuffer(k, "_InfluencerPtrs", influencerPtrBuffer);
            if(influencerBuffer != null) _computeShader.SetBuffer(k, "_Influencers", influencerBuffer);
        }

        if (bridge.oralArmBuffer != null && bridge.oralArmDataList.Count > 0)
        {
            _computeShader.SetBuffer(_kUpdateOralArms, "_OralArmData", bridge.oralArmBuffer);
            _computeShader.SetInt("_OralArmCount", bridge.oralArmDataList.Count);
        }
        
        _isBaked = true;

        if (_medusaRef != null && bridgeBuffer != null)
        {
            _computeShader.SetMatrix("_MedusaMatrix", _medusaRef.transform.localToWorldMatrix);
            _computeShader.SetFloat("_MedusaPhase", _medusaRef.phase);
            _computeShader.SetInt("_InitPhase", 1);
            
            int groupsBridge = Mathf.CeilToInt(bridgeBuffer.count / 64f);
            _computeShader.Dispatch(_kUpdatePos, groupsBridge, 1, 1);
            
            int groupsSprings = Mathf.CeilToInt(springs.Count / 64f);
            if (groupsSprings > 0) _computeShader.Dispatch(_kInitSprings, groupsSprings, 1, 1);
            
            _computeShader.SetInt("_InitPhase", 0);
            
            _prevPosition = _medusaRef.transform.position;
            _prevRotation = _medusaRef.transform.rotation;
        }
    }

    public void Update(float dt)
    {
        if (!_isBaked || _medusaRef == null) return;

        if (dt > 0.05f) dt = 0.05f;

        if (_firstFrame) {
            _prevPosition = _medusaRef.transform.position;
            _prevRotation = _medusaRef.transform.rotation;
            _firstFrame = false;
        }

        Vector3 targetPosition = _medusaRef.transform.position;
        Quaternion targetRotation = _medusaRef.transform.rotation;
        
        _computeShader.SetFloat("_Dampening", waterDrag); 
        _computeShader.SetInt("_InitPhase", 0);
        if (Camera.main != null)
             _computeShader.SetVector("_CamPos", Camera.main.transform.position);

        // ★★★ 2. 關鍵：每一幀將水流參數傳送給 GPU ★★★
        _computeShader.SetVector("_WaterCurrent", waterCurrent);
        _computeShader.SetFloat("_TurbulenceStrength", turbulenceStrength);
        _computeShader.SetFloat("_TurbulenceFreq", turbulenceFreq);
        _computeShader.SetFloat("_TurbulenceSpeed", turbulenceSpeed);
        // 需要時間變數來讓亂流流動
        _computeShader.SetFloat("_Time", Time.time); 

        int threadGroupsVertices = Mathf.CeilToInt(vertices.Count / 64f);
        int threadGroupsBridge = 0;
        if(bridgeBuffer != null) 
            threadGroupsBridge = Mathf.CeilToInt(bridgeBuffer.count / 64f);

        _timeAccumulator += dt;
        int maxSteps = 20; 
        int steps = 0;

        float estimatedSteps = Mathf.Ceil(_timeAccumulator / FIXED_TIME_STEP);
        if (estimatedSteps < 1.0f) estimatedSteps = 1.0f;
        float lerpStep = 1.0f / estimatedSteps;
        float currentLerp = 0.0f;
        
        float simulatedPhase = _medusaRef.phase;
        float phaseSpeed = 0.2f * Mathf.PI * 2.0f;

        while (_timeAccumulator >= FIXED_TIME_STEP && steps < maxSteps)
        {
            currentLerp += lerpStep;
            if (currentLerp > 1.0f) currentLerp = 1.0f;

            Vector3 stepPos = Vector3.Lerp(_prevPosition, targetPosition, currentLerp);
            Quaternion stepRot = Quaternion.Slerp(_prevRotation, targetRotation, currentLerp);
            Matrix4x4 stepMatrix = Matrix4x4.TRS(stepPos, stepRot, _medusaRef.transform.localScale);

            _computeShader.SetMatrix("_MedusaMatrix", stepMatrix);
            _computeShader.SetFloat("_MedusaPhase", simulatedPhase);
            _computeShader.SetFloat("_DeltaTime", FIXED_TIME_STEP);
            simulatedPhase += phaseSpeed * FIXED_TIME_STEP;

            if(threadGroupsBridge > 0)
                _computeShader.Dispatch(_kUpdatePos, threadGroupsBridge, 1, 1);

            _computeShader.Dispatch(_kCalculateForces, threadGroupsVertices, 1, 1);
            _computeShader.Dispatch(_kApplyMovements, threadGroupsVertices, 1, 1);
            
            if (_medusaRef.bridge != null && 
                _medusaRef.bridge.oralArmDataList.Count > 0 && 
                _medusaRef.bridge.oralArmBuffer != null)
            {
                 _computeShader.SetBuffer(_kUpdateOralArms, "_OralArmData", _medusaRef.bridge.oralArmBuffer);
                 _computeShader.SetBuffer(_kUpdateOralArms, "_VertexData", vertexBuffer);
                 _computeShader.SetInt("_OralArmCount", _medusaRef.bridge.oralArmDataList.Count);

                 int threadGroupsOral = Mathf.CeilToInt(_medusaRef.bridge.oralArmDataList.Count / 64f);
                 _computeShader.Dispatch(_kUpdateOralArms, threadGroupsOral, 1, 1);
            }

            _timeAccumulator -= FIXED_TIME_STEP;
            steps++;
        }

        _prevPosition = targetPosition;
        _prevRotation = targetRotation;
    }
    
    public void Teleport(Vector3 delta)
    {
        if (vertexBuffer == null) return;
        VertexData[] nodes = new VertexData[vertexBuffer.count];
        vertexBuffer.GetData(nodes);
        for (int i = 0; i < nodes.Length; i++) nodes[i].position += delta;
        vertexBuffer.SetData(nodes);
        _prevPosition += delta;
    }
    
    public void Dispose()
    {
        vertexBuffer?.Release();
        springBuffer?.Release();
        forceBuffer?.Release();
        bridgeBuffer?.Release();
        influencerPtrBuffer?.Release();
        influencerBuffer?.Release();
    }
}