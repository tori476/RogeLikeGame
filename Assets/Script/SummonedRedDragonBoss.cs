using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SummonedRedDragonBoss : MonoBehaviour
{
    [Header("炎攻撃の設定")]
    public float approachDistance = 8.0f;       // 敵のどれくらい近くまで寄るか
    public float flamePreparationTime = 1.0f;   // 攻撃前の予備動作時間
    public float flameDuration = 3.0f;          // 炎を吐き続ける時間
    public int flameDamage = 20;                // 炎のダメージ量
    public float despawnDelay = 3.0f;           // 攻撃終了後、消滅するまでの時間（炎の煙が消えるのを待つ）

    [Header("エフェクト設定")]
    public GameObject flameEffectPrefab;        // 炎のパーティクルプレハブ（Enemy用ダメージ判定付きのもの）
    public Transform flameSpawnPoint;           // 口元のTransform

    [Header("音声設定")]
    public AudioClip footstepSound;             // 怪獣の足音 (移動時)

    // --- プライベート変数 ---
    private NavMeshAgent agent;
    private Animator anim;
    private Transform targetEnemy;
    private GameObject currentFlameInstance;
    private AudioSource footstepAudioSource;    // 足音専用のAudioSource
    private bool isMoving = false;              // 移動中かどうかのフラグ

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        // 足音専用のAudioSourceを追加
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.loop = true; // ループ再生を有効に
        footstepAudioSource.volume = 1.0f; // 音量を適切に設定
        footstepAudioSource.clip = footstepSound; // 足音クリップを設定

        // 召喚されたら即座に最も近い敵を探して行動を開始
        FindClosestEnemy();
        StartCoroutine(FlameAttackSequence());
    }

    void Update()
    {
        // 移動中の足音再生制御
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            float currentSpeed = agent.velocity.magnitude;

            if (currentSpeed > 0.1f)
            {
                if (!isMoving)
                {
                    isMoving = true;
                    if (footstepAudioSource != null && !footstepAudioSource.isPlaying)
                    {
                        footstepAudioSource.Play();
                    }
                }
            }
            else
            {
                if (isMoving)
                {
                    isMoving = false;
                    if (footstepAudioSource != null && footstepAudioSource.isPlaying)
                    {
                        footstepAudioSource.Stop();
                    }
                }
            }
        }
    }

    // 最も近い「敵(EnemyAI)」を探す（自分自身は除外）
    private void FindClosestEnemy()
    {
        // シーン上のEnemyAIを全て取得し、自分以外を対象にする
        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)
            .Where(e => e.gameObject != this.gameObject).ToArray();

        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (EnemyAI enemy in allEnemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < closestDistance)
            {
                closestDistance = distanceToEnemy;
                closestEnemy = enemy.transform;
            }
        }

        targetEnemy = closestEnemy;
    }

    private IEnumerator FlameAttackSequence()
    {
        // --- 1. 移動フェーズ ---
        // ターゲットが存在する場合、射程距離まで近づく
        if (targetEnemy != null)
        {
            agent.isStopped = false;
            agent.SetDestination(targetEnemy.position);

            // 移動アニメーション（もしあれば）
            anim.SetFloat("Speed", agent.speed);

            // 射程距離に近づくまで待機
            while (targetEnemy != null && Vector3.Distance(transform.position, targetEnemy.position) > approachDistance)
            {
                agent.SetDestination(targetEnemy.position); // 常に最新の位置を追う
                yield return null;
            }
        }
        else
        {
            // 敵がいない場合は少し前進して終わるなどの処理（今回は省略して即攻撃へ）
            Debug.Log("ターゲットが見つかりません。その場で攻撃を開始します。");
        }

        // 移動停止
        agent.isStopped = true;
        anim.SetFloat("Speed", 0f);

        // --- 2. 準備フェーズ ---
        // 敵の方向を向く
        if (targetEnemy != null)
        {
            Vector3 direction = (targetEnemy.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = lookRotation;
        }

        anim.SetTrigger("FlameAttack"); // 攻撃アニメーション開始
        yield return new WaitForSeconds(flamePreparationTime);

        // --- 3. 攻撃（炎放出）フェーズ ---
        StartFlameBreath();

        // 炎を吐いている間、ターゲットの方を向き続ける処理
        float timer = 0f;
        while (timer < flameDuration)
        {
            if (targetEnemy != null)
            {
                // ゆっくり敵の方へ旋回（追尾）
                Vector3 direction = (targetEnemy.position - transform.position).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 2.0f);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // --- 4. 終了フェーズ ---
        EndFlameBreath();
        anim.SetTrigger("Idle"); // 待機に戻す（必要なら）

        Debug.Log("召喚ドラゴンの攻撃終了。消滅待機中...");

        // 炎の煙が消えるまで待ってからDestroy
        yield return new WaitForSeconds(despawnDelay);
        Destroy(gameObject);
    }

    // 炎生成処理
    private void StartFlameBreath()
    {
        if (flameEffectPrefab != null && flameSpawnPoint != null)
        {
            if (currentFlameInstance != null) Destroy(currentFlameInstance);

            currentFlameInstance = Instantiate(flameEffectPrefab, flameSpawnPoint.position, flameSpawnPoint.rotation);
            currentFlameInstance.transform.SetParent(flameSpawnPoint);

            // 【重要】敵用ダメージスクリプトを取得してダメージを設定
            FlameSummonDamageCollider flameScript = currentFlameInstance.GetComponent<FlameSummonDamageCollider>();
            if (flameScript != null)
            {
                flameScript.damage = flameDamage;
                // 自分自身（召喚ドラゴン）を攻撃しないように参照を渡しておくことも可能
                flameScript.owner = this.gameObject;
            }
        }
    }

    // 炎停止処理
    private void EndFlameBreath()
    {
        if (currentFlameInstance != null)
        {
            ParticleSystem ps = currentFlameInstance.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop();

            // 親子関係を解除（頭が動いても煙はその場に残るように）
            currentFlameInstance.transform.SetParent(null);

            // 少し後に完全削除
            Destroy(currentFlameInstance, 3.0f);
            currentFlameInstance = null;
        }
    }

    private void OnDestroy()
    {
        if (currentFlameInstance != null) Destroy(currentFlameInstance);
    }
}