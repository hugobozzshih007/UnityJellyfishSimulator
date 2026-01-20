// 對應 src/medusaBell.js
public class MedusaBell
{
    public MedusaBellTop top;
    
    public MedusaBellBottom bottom;
    public MedusaBellMargin margin;
    
    public MedusaBellGeometry geometryOutside;
    public MedusaBellGeometry geometryInside;

    private Medusa _medusa;

    public MedusaBell(Medusa medusa)
    {
        _medusa = medusa;
        
        // JS: this.geometryOutside = new MedusaBellGeometry(medusa, true);
        geometryOutside = new MedusaBellGeometry(medusa, true);
        
        // JS: this.geometryInside = new MedusaBellGeometry(medusa, false);
        geometryInside = new MedusaBellGeometry(medusa, false);

        // JS: this.top = new MedusaBellTop(medusa);
        top = new MedusaBellTop(medusa);
        bottom = new MedusaBellBottom(medusa);
        margin = new MedusaBellMargin(medusa);
    }

    public void CreateGeometry()
    {
        // 這裡我們目前只呼叫 Top
        top.CreateGeometry();
        bottom.CreateGeometry();
        margin.CreateGeometry();
        // 原案最後會呼叫 bake
        geometryOutside.BakeGeometry();
        geometryInside.BakeGeometry(); 
    }
}