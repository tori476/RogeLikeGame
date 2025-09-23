using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要

public class EnemyAI : MonoBehaviour
{
    [Header("ステータス")]
    public int health = 100;
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

    public void TakeDamage(int damage)
    {
        // 体力を減らす
        health -= damage;
        Debug.Log(gameObject.name + " の残り体力: " + health);

        // 体力が0以下になったら
        if (health <= 0)
        {
            Die();
        }
    }

    // 死亡時の処理を行うメソッド
    private void Die()
    {
        Debug.Log(gameObject.name + " は倒された！");
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
}
