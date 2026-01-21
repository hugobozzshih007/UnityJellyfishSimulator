using UnityEngine;

[ExecuteAlways] // 讓您在編輯器不按 Play 也能看到燈光變化 (選用)
public class DepthLightController : MonoBehaviour
{
    [Header("References")]
    public Light directionalLight;
    public Transform jellyfishTarget;
    public Material bellOutter;

    [Header("Depth Settings")]
    public float deepestY = -50f;
    public float surfaceY = 0f;

    [Header("Sun Intensity")]
    public float minLight = 0.5f;
    public float maxLight = 2.0f;

    [Header("Global Caustic Settings")]
    // ★ 關鍵：這個字串必須跟 Shader Graph 裡的 Reference 一模一樣
    public string globalCausticName = "_CausticIntensity"; 
    public float minCaustic = 0.0f;
    public float maxCaustic = 3.0f;

    void Update()
    {
        if (jellyfishTarget == null) return;

        // 1. 計算深度 (0 = 深淵, 1 = 水面)
        float t = Mathf.InverseLerp(deepestY, surfaceY, jellyfishTarget.position.y);

        // 2. 控制 Directional Light
        if (directionalLight != null)
        {
            directionalLight.intensity = Mathf.Lerp(minLight, maxLight, t);
        }

        // 3. ★ 控制全域 Shader 變數
        // 這行程式碼會影響場景中 "所有" 使用這個變數名稱的材質
        // 不管是 Bell, OralArms, Tentacles，甚至是剛生成的 Plankton
        float currentCaustic = Mathf.Lerp(minCaustic, maxCaustic, t);
        if(bellOutter != null)
            bellOutter.SetFloat(globalCausticName, currentCaustic);
    }
}