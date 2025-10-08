using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要
using System.Collections;
using System;

public class EnemyAI : MonoBehaviour
{
    [Header("ステータス")]
    public int health = 100;

    [Header("攻撃設定")]
    public float attackRange = 2.0f;    // 攻撃を開始するプレイヤーとの距離
    public float attackCooldown = 1.5f; // 攻撃後の待ち時間（秒）
    public int attackDamage = 1;        // 敵の攻撃力を1に固定
    private float lastAttackTime = 0f;  // 最後に攻撃した時間

    [Header("エフェクト設定")]
    public float knockbackForce = 10f;    // ノックバックの強さ
    public float knockbackDuration = 0.4f; // ノックバックする時間
    protected Coroutine knockbackCoroutine;
    protected NavMeshAgent agent;
    protected Transform player;

    protected bool isActivated = false;

    public event Action<EnemyAI> OnEnemyDied;

    protected virtual void Start()
    {
        // 自分にアタッチされているNavMeshAgentを取得
        agent = GetComponent<NavMeshAgent>();

        // "Player" タグがついたオブジェクト（プレイヤー）を探して、そのTransformを取得
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }

        // 念のため、攻撃力を1に強制設定
        attackDamage = 1;
        Debug.Log($"{gameObject.name} の攻撃力を {attackDamage} に設定しました");
    }

    protected virtual void Update()
    {
        if (!isActivated || player == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || knockbackCoroutine != null)
        {
            return;
        }

        // プレイヤーとの距離を計算
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 攻撃範囲内にいて、クールダウンが終わっていれば攻撃
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            AttackPlayer();
            lastAttackTime = Time.time;
            agent.isStopped = true; // 攻撃中は停止
        }
        else if (distanceToPlayer > attackRange)
        {
            // プレイヤーが見つかっていれば、その位置を目的地に設定し続ける
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    // プレイヤーを攻撃するメソッド
    private void AttackPlayer()
    {
        Debug.Log($"{gameObject.name} がプレイヤーを攻撃！攻撃力: {attackDamage}");

        PlayerHP playerHP = player.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(attackDamage);
            Debug.Log($"{gameObject.name} がプレイヤーに {attackDamage} のダメージを与えた！");
        }
    }

    public void ActivateEnemy()
    {
        // 既に起動済みなら何もしない
        if (isActivated) return;

        isActivated = true;
        // 起動したらNavMeshAgentを有効にして、追跡を開始できるようにする
        if (agent != null)
        {
            agent.enabled = true;
        }
        Debug.Log(this.gameObject.name + " が起動しました！");
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
        OnEnemyDied?.Invoke(this);
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
}
