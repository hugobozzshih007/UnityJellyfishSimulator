using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices; // 記得加入這個

public class MedusaVerletBridge
{
    private VerletPhysics _physics;
    
    // ★★★ 加入 StructLayout 確保數據對齊 (40 bytes)
    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeData
    {
        public int vertexId;
        public int medusaId;
        public float zenith;
        public float azimuth;
        public Vector4 offset;
        public float isBottom;
        public float directionalOffset;
        public float isFixed;
        public float padding; // ★★★ 新增這行：湊滿 48 bytes (16的倍數)
    }
    
    // ★ 新增：對應 Shader 的 OralArmBridgeData (觸手用)
    [StructLayout(LayoutKind.Sequential)]
    public struct OralArmBridgeData
    {
        // 替代 Vector4Int，確保記憶體連續且無相容性問題
        public int v0;
        public int v1;
        public int v2;
        public int v3; 
        
        public Vector4 sideData;       
        
        public int visualVertexId;
        public int pad0;
        public int pad1;
        public int pad2; // 湊齊 48 bytes
    }

    public List<BridgeData> bridgeVertices = new List<BridgeData>();
    
    public List<OralArmBridgeData> oralArmDataList = new List<OralArmBridgeData>();
    public ComputeBuffer oralArmBuffer;

    public MedusaVerletBridge(VerletPhysics physics)
    {
        _physics = physics;
    }

    public int RegisterMedusa(Medusa medusa)
    {
        return 0; 
    }

    public void RegisterVertex(int medusaId, int vertexId, float zenith, float azimuth, bool isBottom, Vector3 offset, float directionalOffset, bool isFixed)
    {
        bridgeVertices.Add(new BridgeData
        {
            vertexId = vertexId,
            medusaId = medusaId,
            zenith = zenith,
            azimuth = azimuth,
            offset = new Vector4(offset.x, offset.y, offset.z, 0),
            isBottom = isBottom ? 1.0f : 0.0f,
            directionalOffset = directionalOffset,
            // ★★★ 強制修正：isFixed (固定) = 1.0f, Free (自由) = 0.0f
            isFixed = isFixed ? 1.0f : 0.0f,
            padding = 0 // ★★★ 初始化為 0
        });
    }
    
    // ★ 新增：註冊觸手蒙皮數據
    public void RegisterOralArmSegment(int v0, int v1, int v2, int v3, Vector3 side, float width, int visualIndex)
    {
        OralArmBridgeData data = new OralArmBridgeData();
        data.v0 = v0; 
        data.v1 = v1; 
        data.v2 = v2; 
        data.v3 = v3;
        data.sideData = new Vector4(side.x, side.y, side.z, width);
        data.visualVertexId = visualIndex;
        data.pad0 = 0; data.pad1 = 0; data.pad2 = 0;
        
        oralArmDataList.Add(data);
    }
    
    public void Bake() 
    {
        if (oralArmDataList.Count > 0)
        {
            if (oralArmBuffer != null) oralArmBuffer.Release();
            oralArmBuffer = new ComputeBuffer(oralArmDataList.Count, Marshal.SizeOf(typeof(OralArmBridgeData)));
            oralArmBuffer.SetData(oralArmDataList.ToArray());
        }
    }
    
    public void Dispose()
    {
        if (oralArmBuffer != null) oralArmBuffer.Release();
    }
}