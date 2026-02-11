using UnityEngine;

public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance;
    
    [Header("Environment Settings")]
    public float sphereRadius = 50f;     // 環境球半徑
    public float safeMargin = 5f;        // 邊界安全距離
    public float eatDistance = 1.5f;

    [Header("Constraint Settings")]
    [Tooltip("限制餌食相對於參考點向下掉落的垂直距離上限")]
    public float downYLimit = 20f;       

    [Header("Current Target")]
    public Vector3 currentFoodPosition;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 取得場景中的水母作為第一個餌食的參考點
        Medusa m = FindObjectOfType<Medusa>();
        Vector3 startPos = (m != null) ? m.transform.position : Vector3.zero;
        SpawnNewFood(startPos);
    }

    public void SpawnNewFood(Vector3 referencePos)
    {
        float effectiveRadius = sphereRadius - safeMargin;
        Vector3 randomPos;
        
        // 使用迴圈確保生成的點同時滿足球體範圍與您的自定義規則
        int maxAttempts = 10;
        int attempts = 0;
        
        do {
            randomPos = Random.insideUnitSphere * effectiveRadius;
            float deltaY = randomPos.y - referencePos.y;

            // ★ 核心規則整合 ★
            // 1. 如果是向下 (deltaY < 0)
            if (deltaY < 0)
            {
                // 規則 A: 垂直距離不能超過 downYLimit
                if (Mathf.Abs(deltaY) > downYLimit)
                {
                    randomPos.y = referencePos.y - downYLimit;
                    deltaY = -downYLimit;
                }

                // 規則 B: [新增] 水平距離 (X) 必須大於垂直向下距離 (Y)
                // 這裡我們取 XZ 平面的偏移量來檢查
                float horizontalDist = Vector2.Distance(new Vector2(randomPos.x, randomPos.z), new Vector2(referencePos.x, referencePos.z));
                float verticalDist = Mathf.Abs(deltaY);

                if (horizontalDist < verticalDist)
                {
                    // 如果水平距離太小，補足水平偏移，確保角度平緩
                    Vector2 dirXZ = new Vector2(randomPos.x - referencePos.x, randomPos.z - referencePos.z).normalized;
                    if (dirXZ == Vector2.zero) dirXZ = Random.insideUnitCircle.normalized; // 防止重疊
                    
                    float newHorizontalDist = verticalDist + 2.0f; // 確保 X > Y
                    randomPos.x = referencePos.x + dirXZ.x * newHorizontalDist;
                    randomPos.z = referencePos.z + dirXZ.y * newHorizontalDist;
                }
            }
            
            attempts++;
            // 只要還在球體內就跳出，否則重試（或強制縮回邊界）
        } while (randomPos.magnitude > effectiveRadius && attempts < maxAttempts);

        // 最後邊界保險
        if (randomPos.magnitude > effectiveRadius)
        {
            randomPos = randomPos.normalized * effectiveRadius;
        }

        currentFoodPosition = randomPos;
    }

    public void CheckIfReached(Vector3 medusaPos)
    {
        if (Vector3.Distance(medusaPos, currentFoodPosition) < eatDistance)
        {
            SpawnNewFood(medusaPos);
        }
    }
}