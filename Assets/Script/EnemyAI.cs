using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要

public class EnemyAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;

    void Start()
    {
        // 自分にアタッチされているNavMeshAgentを取得
        agent = GetComponent<NavMeshAgent>();

        // "Player" タグがついたオブジェクト（プレイヤー）を探して、そのTransformを取得
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    void Update()
    {
        // プレイヤーが見つかっていれば、その位置を目的地に設定し続ける
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }
}
