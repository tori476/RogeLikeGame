using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要
using System.Collections;
using System;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("ステータス")]
    public int health = 100;

    [Header("攻撃設定")]
    public float attackRange = 2.0f;    // 攻撃を開始するプレイヤーとの距離
    public float attackCooldown = 1.5f; // 攻撃後の待ち時間（秒）
    public int attackDamage = 1;        // 敵の攻撃力を1に固定
    protected float lastAttackTime = 0f;  // 最後に攻撃した時間（protected に変更）

    [Header("効果音設定")]
    public AudioClip attackSound;       // 攻撃時の効果音
    [Range(0f, 1f)]
    public float attackSoundVolume = 0.8f; // 攻撃音の音量
    protected AudioSource audioSource;

    [Header("ドロップアイテム設定")]
    public GameObject heartPrefab;      // ハートのプレハブをインスペクターから設定
    [Range(0.0f, 1.0f)]
    public float heartDropChance = 0.1f; // ハートをドロップする確率 (%)

    [Header("エフェクト設定")]
    public float knockbackForce = 10f;    // ノックバックの強さ
    public float knockbackDuration = 0.4f; // ノックバックする時間
    protected Coroutine knockbackCoroutine;
    protected NavMeshAgent agent;
    protected Transform player;

    protected Rigidbody rb;

    protected bool isActivated = false;

    public event Action<EnemyAI> OnEnemyDied;

    public event Action<int> OnHealthChanged;

    protected virtual void Start()
    {
        // 自分にアタッチされているNavMeshAgentを取得
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // AudioSourceコンポーネントを取得、なければ追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

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

        // 攻撃効果音を再生
        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound, attackSoundVolume);
        }

        PlayerHP playerHP = player.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(attackDamage, true); // EnemyAIからの攻撃であることを示す
            Debug.Log($"{gameObject.name} がプレイヤーに {attackDamage} のダメージを与えた！");
        }
    }

    public virtual void ActivateEnemy()
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

    public virtual void TakeDamage(int damage, Transform attacker)
    {
        // 体力を減らす
        health -= damage;
        OnHealthChanged?.Invoke(health);
        Debug.Log(gameObject.name + " の残り体力: " + health);

        // ノックバック処理（NavMeshAgentが存在し、かつ有効な場合のみ）
        if (agent != null && knockbackCoroutine == null && attacker != null)
        {
            knockbackCoroutine = StartCoroutine(Knockback(attacker));
        }

        // 体力が0以下になったら
        if (health <= 0)
        {
            Die();
        }
    }

    protected IEnumerator Knockback(Transform attacker)
    {
        // NavMeshAgentが存在しない、または無効な場合は何もしない
        if (agent == null)
        {
            yield break;
        }

        // AIの移動を一時的に停止
        if (agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Rigidbodyを物理演算の対象にする
        rb.isKinematic = false;

        // 攻撃者から自分への方向ベクトルを計算（吹き飛ぶ方向）
        Vector3 direction = (transform.position - attacker.position).normalized;
        direction.y = 0; // 上下には吹き飛ばないようにする

        // 既存の速度をリセットしてから力を加える
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * knockbackForce, ForceMode.Impulse);

        // ノックバック時間待機
        yield return new WaitForSeconds(knockbackDuration);

        // Rigidbodyの物理演算を停止し、速度をゼロにする
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // NavMeshAgentが存在し、かつ有効な場合のみ再開
        if (agent != null)// && agent.isOnNavMesh
        {

        }
        agent.isStopped = false;
        agent.enabled = true;
        knockbackCoroutine = null; // コルーチンが終了したことを示す
    }

    // 死亡時の処理を行うメソッド
    private void Die()
    {
        Debug.Log(gameObject.name + " は倒された！");
        if (heartPrefab != null && UnityEngine.Random.value <= heartDropChance)
        {
            Instantiate(heartPrefab, new Vector3(transform.position.x, transform.position.y + 2, transform.position.z), Quaternion.identity);
            Debug.Log(gameObject.name + " がハートをドロップしました！");
        }
        OnEnemyDied?.Invoke(this);
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
}
