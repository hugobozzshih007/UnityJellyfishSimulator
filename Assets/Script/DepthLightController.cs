using UnityEngine;

[ExecuteAlways]
public class DepthLightController : MonoBehaviour
{
    [Header("References")]
    public Light directionalLight;
    public Transform jellyfishTarget;

    [Header("Depth Settings")]
    public float deepestY = -50f;
    public float surfaceY = 50f; 

    [Header("Angle Settings")]
    public float maxVisibleAngle = 160f; 
    public Vector3 lightSourceDirection = Vector3.up; 

    [Header("Caustic Intensity")]
    public string globalCausticName = "_CausticIntensity"; 
    public float minCaustic = 0.0f;
    public float maxCaustic = 1.0f;

    [Header("Materials")] 
    public Material outerBell;
    public Material innerBell;

    void Update()
    {
        if (jellyfishTarget == null) return;

        // 1. 深度權重 (0 = 深處, 1 = 表面)
        float t_depth = Mathf.InverseLerp(deepestY, surfaceY, jellyfishTarget.position.y);

        // 2. 角度權重 (0 = 背光, 1 = 正對光)
        float angle = Vector3.Angle(jellyfishTarget.forward, lightSourceDirection);
        float t_angle = 0f;
        if (angle <= maxVisibleAngle)
        {
            t_angle = 1.0f - (angle / maxVisibleAngle);
        }

        // 3. 【核心修正】取兩者之中的最小值作為權重
        // 這樣只要有一方過低，Caustic 就會消失；反之則能維持較佳亮度
        float outerlWeight = Mathf.Min(t_depth, t_angle);
        float outerIntensity = Mathf.Lerp(minCaustic, maxCaustic, outerlWeight);
        Debug.LogWarning("intensity: "+ outerIntensity.ToString());
        float rT_angle = 1.0f - t_angle; 
        float innerlWeight = Mathf.Min(t_depth, rT_angle);
        float innerIntensity = Mathf.Lerp(minCaustic, maxCaustic, innerlWeight);
        Debug.LogWarning("intensity: "+ innerIntensity.ToString());
        // 4. 全域套用
        if(outerBell != null)
            outerBell.SetFloat(globalCausticName, outerIntensity);
        if(innerBell != null)
            innerBell.SetFloat(globalCausticName, innerIntensity);

        if (directionalLight != null)
        {
            directionalLight.intensity = Mathf.Lerp(0.5f, 2.0f, t_depth);
        }
    }
}