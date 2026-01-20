using UnityEngine;

public class VerletPhysicsVisualizer : MonoBehaviour
{
    public Medusa medusa; // 引用主腳本
    public bool showSprings = true;
    public bool showVertices = false;

    private void OnDrawGizmos()
    {
        if (medusa == null || medusa.physics == null) return;

        // 畫彈簧 (綠線)
        if (showSprings && medusa.physics.springs != null)
        {
            Gizmos.color = Color.green;
            foreach (var spring in medusa.physics.springs)
            {
                // 注意：這裡直接讀取我們上一回寫的 VerletPhysics 數據
                // 因為還沒上 GPU，目前數據還在 CPU List 裡，可以直接畫
                if (spring.indexA < medusa.physics.vertices.Count && spring.indexB < medusa.physics.vertices.Count)
                {
                    Vector3 posA = medusa.physics.vertices[spring.indexA].position;
                    Vector3 posB = medusa.physics.vertices[spring.indexB].position;
                    Gizmos.DrawLine(transform.TransformPoint(posA), transform.TransformPoint(posB));
                }
            }
        }

        // 畫頂點 (紅點)
        if (showVertices && medusa.physics.vertices != null)
        {
            Gizmos.color = Color.red;
            foreach (var v in medusa.physics.vertices)
            {
                Gizmos.DrawSphere(transform.TransformPoint(v.position), 0.02f);
            }
        }
    }
}