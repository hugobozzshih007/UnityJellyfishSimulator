using UnityEngine;
using System.Collections.Generic;

// 所有水母模組的基底類別
public abstract class MedusaModule : MonoBehaviour
{
    protected Medusa owner;
    public abstract void Initialize(Medusa medusa);
    public abstract void UpdateModule(float dt);
}

// 鐘體頂部基底：需提供頂點行資訊供裙邊連接
public abstract class MedusaBellTopBase : MedusaModule 
{
    public abstract List<List<MedusaBellGeometry.VertexInfo>> GetVertexRows();
}

// 鐘體內殼基底
public abstract class MedusaBellBottomBase : MedusaModule { }

// 裙邊基底：需提供物理行資訊供觸手連接
public abstract class MedusaBellMarginBase : MedusaModule 
{
    public abstract List<List<int>> GetMarginRows();
    public abstract int GetMarginWidth();
}

// 口腕基底
public abstract class MedusaOralArmsBase : MedusaModule { }

// 觸手基底
public abstract class MedusaTentaclesBase : MedusaModule { }