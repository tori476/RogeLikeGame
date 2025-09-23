using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    [Header("ステータス")]
    public int health = 100;

    [Header("エフェクト設定")]
    public float knockbackForce = 10f;    // ノックバックの強さ
    public float knockbackDuration = 0.4f; // ノックバックする時間
    private Coroutine knockbackCoroutine;
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
        if (player != null && agent.isActiveAndEnabled && agent.isOnNavMesh && knockbackCoroutine == null)
        {
            agent.SetDestination(player.position);
        }
    }

    public void TakeDamage(int damage, Transform attacker)
    {
        // 体力を減らす
        health -= damage;
        Debug.Log(gameObject.name + " の残り体力: " + health);
        if (knockbackCoroutine == null)
        {
            knockbackCoroutine = StartCoroutine(Knockback(attacker));
        }


        // 体力が0以下になったら
        if (health <= 0)
        {
            Die();
        }
    }
    private IEnumerator Knockback(Transform attacker)
    {
        // AIの移動を一時的に停止
        agent.enabled = false;

        // 攻撃者から自分への方向ベクトルを計算（吹き飛ぶ方向）
        Vector3 direction = (transform.position - attacker.position).normalized;
        direction.y = 0; // 上下には吹き飛ばないようにする

        float elapsedTime = 0f;
        while (elapsedTime < knockbackDuration)
        {
            // 計算した方向へ、力を加えながら後退させる
            transform.position += direction * knockbackForce * Time.deltaTime;
            elapsedTime += Time.deltaTime;
            yield return null; // 1フレーム待機
        }

        // AIの移動を再開
        agent.enabled = true;
        knockbackCoroutine = null; // コルーチンが終了したことを示す
    }

    // 死亡時の処理を行うメソッド
    private void Die()
    {
        Debug.Log(gameObject.name + " は倒された！");
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
}
