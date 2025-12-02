using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SummonedOrcBoss : MonoBehaviour
{
    [Header("オーク固有の設定: 範囲攻撃(スマッシュ)")]

    public float approachDistance = 3.0f;       // 敵のどれくらい近くまで寄るか
    public float smashCooldown = 5.0f;
    public int smashDamage = 25;
    public float smashRadius = 5.0f; // 攻撃範囲の半径
    public GameObject smashEffectPrefab; // 地面を叩きつけた時のエフェクト

    public GameObject smashPosition; // エフェクトを出す場所

    public float AttackPreparationTime = 1.0f;



    // --- プライベート変数 ---
    private NavMeshAgent agent;
    private Animator anim;
    private Transform targetEnemy;
    private GameObject currentFlameInstance;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        // 召喚されたら即座に最も近い敵を探して行動を開始
        FindClosestEnemy();
        StartCoroutine(SmashAttackSequence());
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

    private IEnumerator SmashAttackSequence()
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

        anim.SetTrigger("SmashAttack"); // 攻撃アニメーション開始
        yield return new WaitForSeconds(AttackPreparationTime);




        // --- 4. 終了フェーズ ---

        Destroy(gameObject);
    }

    public void ExecuteSmashAttack()
    {
        // エフェクト生成
        if (smashEffectPrefab != null)
        {
            Instantiate(smashEffectPrefab, smashPosition.transform.position, Quaternion.identity);
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, smashRadius);

        // 範囲ダメージ判定 (自分中心の円形範囲)
        foreach (var hitCollider in hitColliders)
        {
            // 衝突したのが敵（かつ自分自身ではない）ならダメージを与える
            EnemyAI enemy = hitCollider.GetComponent<EnemyAI>();
            if (enemy != null && enemy.gameObject != this.gameObject)
            {
                enemy.TakeDamage(smashDamage, transform);
                Debug.Log($"{enemy.name} に {smashDamage} のダメージを与えた！");
            }
        }
    }
}