using UnityEngine;

public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance;
    
    [Header("Environment Settings")]
    public float sphereRadius = 50f;     // 環境球半徑
    public float safeMargin = 5f;        // 邊界安全距離，避免餌食貼在牆上
    public float eatDistance = 1.5f;

    [Header("Current Target")]
    public Vector3 currentFoodPosition;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnNewFood();
    }

    public void SpawnNewFood()
    {
        // 使用 Random.insideUnitSphere 產生球體內的隨機方向
        // 乘以 (半徑 - 安全邊距) 確保不會生在球殼邊緣
        float effectiveRadius = sphereRadius - safeMargin;
        currentFoodPosition = Random.insideUnitSphere * effectiveRadius;
    }

    public void CheckIfReached(Vector3 medusaPos)
    {
        if (Vector3.Distance(medusaPos, currentFoodPosition) < eatDistance)
        {
            SpawnNewFood();
        }
    }
}