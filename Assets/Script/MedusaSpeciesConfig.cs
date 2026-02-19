using UnityEngine;

[CreateAssetMenu(fileName = "NewSpeciesConfig", menuName = "Pillowbit/Medusa/Species Config")]
public class MedusaSpeciesConfig : ScriptableObject
{
    [Header("鐘體組成")]
    public MedusaBellTopBase bellTopPrefab;    
    public MedusaBellBottomBase bellBottomPrefab;
    public MedusaBellMarginBase bellMarginPrefab;

    [Header("附屬器官")]
    public MedusaOralArmsBase oralArmsPrefab;
    public MedusaTentaclesBase tentaclesPrefab;

    [Header("物種通用參數")]
    public int subdivisions = 40;
    public float moveSpeedMultiplier = 2.0f;
    
    [Header("Smooth Frequency")]
    public float frequencyBoost = 3.0f;
    public float currentFreqMultiplier = 1.0f; // 當前頻率倍率
    public float freqLerpSpeed = 2.0f;       // 頻率變化的平滑速度
    
    [Header("Materials")]
    public Material jellyfishMaterialOutside;
    public Material jellyfishMaterialInside;
    public Material oralArmsMaterial;
    public Material tentaclesMaterial;
    
}