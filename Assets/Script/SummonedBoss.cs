// SummonedBoss.cs
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq; // LINQを使って最も近い敵を探すために必要

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class SummonedBoss : MonoBehaviour
{
    [Header("突進攻撃の設定")]
    public float chargePreparationTime = 1.5f;  // 突進前の溜め時間（秒）
    public float chargeSpeed = 20.0f;           // 突進の速度
    public int chargeDamage = 50;               // 突進の攻撃力
    public float chargeImpactRadius = 2.5f;     // 突進がヒットした際のダメージ範囲
    public GameObject chargeIndicatorPrefab;    // 突進先の地面に表示するインジケーターのプレハブ
    public float despawnDelay = 2.0f;           // 攻撃後に消滅するまでの時間

    // --- プライベート変数 ---
    private NavMeshAgent agent;
    private Animator anim;
    private Transform targetEnemy;
    private GameObject chargeIndicatorInstance;

    void Awake()
    {
        // 必要なコンポーネントを自動で取得
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        // 最も近い敵を探してターゲットに設定
        FindClosestEnemy();
    }

    // PlayerControllerから呼び出されるメソッド
    void Update()
    {
        // 突進攻撃のコルーチンを開始
        StartCoroutine(ChargeAttackCoroutine());
    }

    // シーン内にいる"Enemy"タグがついたオブジェクトの中から、最も近いものを探す
    private void FindClosestEnemy()
    {
        // 自分自身を除外してすべての敵を見つける
        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Where(e => e.gameObject != this.gameObject).ToArray();

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

    private IEnumerator ChargeAttackCoroutine()
    {
        // --- 1. 準備フェーズ ---
        Vector3 targetPosition;

        if (targetEnemy != null)
        {
            // 敵がいる場合は、その位置をターゲットとする
            targetPosition = targetEnemy.position;
            // 敵の方向をゆっくりと向く
            Quaternion targetRotation = Quaternion.LookRotation(targetPosition - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1.0f);
        }
        else
        {
            // 敵がいない場合は、自分の前方10mをターゲットとする
            targetPosition = transform.position + transform.forward * 10f;
            Debug.Log("攻撃対象の敵が見つからなかったため、前方に突進します。");
        }

        // NavMeshAgentを一旦停止
        if (agent.isOnNavMesh) agent.isStopped = true;

        // 突進先の地面にインジケーターを表示
        if (chargeIndicatorPrefab != null)
        {
            chargeIndicatorInstance = Instantiate(chargeIndicatorPrefab, targetPosition, Quaternion.identity);
        }

        // 溜めアニメーションを再生 (Animatorに "Charge" というトリガーがある想定)
        anim.SetTrigger("Charge");

        // 溜め時間だけ待機
        yield return new WaitForSeconds(chargePreparationTime);

        // --- 2. 実行フェーズ ---
        // インジケーターを消去
        if (chargeIndicatorInstance != null)
        {
            Destroy(chargeIndicatorInstance);
        }

        // 目標地点に向かって直接移動させる
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, chargeSpeed * Time.deltaTime);

        // 突進アニメーションを再生 (Animatorに "ChargeAttack" というトリガーがある想定)
        anim.SetTrigger("ChargeAttack");

        // 目的地に到着するまで待機（またはタイムアウト）
        float chargeStartTime = Time.time;
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            // 5秒以上突進し続けたら、強制的に終了する（壁にハマる対策）
            if (Time.time - chargeStartTime > 5.0f)
            {
                Destroy(gameObject);
            }
            yield return null;
        }

        // --- 3. インパクト（ダメージ処理）フェーズ ---
        Debug.Log("突進が終了。インパクトダメージを与えます。");

        // 突進アニメーションを終了させる
        anim.ResetTrigger("ChargeAttack");

        // 移動を完全に停止
        if (agent.isOnNavMesh) agent.ResetPath();

        // 自分の周囲(chargeImpactRadiusの範囲)にいるコライダーを全て取得
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, chargeImpactRadius);
        foreach (var hitCollider in hitColliders)
        {
            // 衝突したのが敵（かつ自分自身ではない）ならダメージを与える
            EnemyAI enemy = hitCollider.GetComponent<EnemyAI>();
            if (enemy != null && enemy.gameObject != this.gameObject)
            {
                enemy.TakeDamage(chargeDamage, transform);
                Debug.Log($"{enemy.name} に {chargeDamage} の突進ダメージを与えた！");
            }
        }

        // --- 4. 消滅フェーズ ---
        // 少し待ってから自分自身をシーンから削除
        yield return new WaitForSeconds(despawnDelay);
        Destroy(gameObject);
    }

    // Gizmoで攻撃範囲をエディタ上に表示する
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chargeImpactRadius);
    }
}