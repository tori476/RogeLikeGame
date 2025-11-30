using UnityEngine;
using System.Collections;

public class BossAI_Orc : BossAI
{
    [Header("オーク固有の設定: 範囲攻撃(スマッシュ)")]
    public float smashCooldown = 5.0f;
    public int smashDamage = 25;
    public float smashRadius = 5.0f; // 攻撃範囲の半径
    public GameObject smashEffectPrefab; // 地面を叩きつけた時のエフェクト

    public GameObject smashPosition; // エフェクトを出す場所

    [Header("通常攻撃の角度制限")]
    [Range(0, 180)]
    public float attackAngle = 45.0f; // 前方中心から左右に何度まで許容するか

    [Header("突進スキル (タックル)")]
    public float chargeRange = 15.0f;
    public float chargePrepTime = 1.0f; // オークは予備動作短め
    public float chargeSpeed = 15.0f;
    public int chargeDamage = 30;
    public float chargeCooldown = 8.0f;
    public float chargeProbability = 0.4f; // 突進確率
    public GameObject chargeIndicatorPrefab;
    public float chargeImpactRadius = 2.0f;

    // ステートマシン
    private enum OrcState { Chasing, Attacking, PreparingCharge, Charging, Cooldown }
    private OrcState currentState = OrcState.Chasing;

    private Animator anim;
    private Vector3 chargeTargetPos;
    private GameObject currentIndicator;

    protected override void Start()
    {
        base.Start();
        anim = GetComponent<Animator>();
        currentState = OrcState.Chasing;
    }

    protected override void HandleBehavior()
    {
        UpdateMoveAnimation();

        // 突進中以外はプレイヤーの方を向く
        if (currentState == OrcState.Chasing)
        {
            LookAtPlayer();
        }

        switch (currentState)
        {
            case OrcState.Chasing:
                ProcessChasing();
                break;
            case OrcState.Charging:
                ProcessCharging();
                break;
        }
    }

    private void UpdateMoveAnimation()
    {
        if (anim == null) return;
        float currentSpeed = 0f;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            currentSpeed = agent.velocity.magnitude;
        }
        anim.SetFloat("Speed", currentSpeed, 0.1f, Time.deltaTime);
    }

    private void LookAtPlayer()
    {
        if (player == null) return;
        Vector3 dir = (player.position - transform.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
    }

    private void ProcessChasing()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.SetDestination(player.position);

        float dist = Vector3.Distance(transform.position, player.position);

        // --- 1. 突進判定 (距離が離れている時) ---
        if (dist <= chargeRange && dist > attackRange)
        {
            if (UnityEngine.Random.value < chargeProbability)
            {
                StartCoroutine(Routine_PrepareCharge());
                return;
            }
        }

        // --- 2. 近距離攻撃判定 ---
        if (dist <= attackRange)
        {
            // 範囲攻撃（スマッシュ）は角度関係なく、距離が近ければ発動チャンスあり
            // ただしランダム性を持たせる
            bool trySmash = (UnityEngine.Random.value > 0.95f); // %の確率で範囲攻撃

            if (trySmash)
            {
                StartCoroutine(Routine_Smash_Attack());
            }
            else
            {
                // 通常攻撃は正面に捉えている必要がある
                Vector3 dirToPlayer = player.position - transform.position;
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle <= attackAngle)
                {
                    StartCoroutine(Routine_Normal_Attack());
                }
            }
        }
    }

    private void ProcessCharging()
    {
        // 突進移動処理
        transform.position = Vector3.MoveTowards(transform.position, chargeTargetPos, chargeSpeed * Time.deltaTime);

        // 目標地点付近、または衝突判定
        float distToDest = Vector3.Distance(transform.position, chargeTargetPos);
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // プレイヤーにヒットしたか、目的地に着いた場合
        if (distToPlayer <= chargeImpactRadius || distToDest < 0.5f)
        {
            // ヒット判定
            if (distToPlayer <= chargeImpactRadius)
            {
                player.GetComponent<PlayerHP>()?.TakeDamage(chargeDamage);
                Debug.Log("オークのタックルがヒット！");
            }

            agent.enabled = true; // NavMeshAgent復帰
            StartCoroutine(Routine_Cooldown(chargeCooldown));
        }
    }

    // --- コルーチン群 ---

    // 通常攻撃（武器を振るなど）
    private IEnumerator Routine_Normal_Attack()
    {
        currentState = OrcState.Attacking;
        agent.isStopped = true;
        anim.SetTrigger("Attack"); // 通常攻撃アニメーション

        // アニメーションが終わるのを待つ簡易的な実装（またはイベント待ち）
        // ここではアニメーションイベント(DealDamageToPlayer)が呼ばれるのを期待して
        // 次の行動までの待機時間を入れる
        yield return new WaitForSeconds(1.0f);

        StartCoroutine(Routine_Cooldown(attackCooldown));
    }

    // 範囲攻撃（地面叩きつけ）
    private IEnumerator Routine_Smash_Attack()
    {
        currentState = OrcState.Attacking;
        agent.isStopped = true;
        anim.SetTrigger("SmashAttack"); // 範囲攻撃用アニメーションTrigger

        // アニメーションイベント ExecuteSmashAttack() が呼ばれるのを待つ
        // 硬直時間は長めに設定
        yield return new WaitForSeconds(1.5f);

        StartCoroutine(Routine_Cooldown(smashCooldown));
    }

    // 突進準備
    private IEnumerator Routine_PrepareCharge()
    {
        currentState = OrcState.PreparingCharge;
        agent.isStopped = true;
        anim.SetTrigger("PrepareCharge"); // 構え

        chargeTargetPos = player.position;

        // 突進ラインの表示
        if (chargeIndicatorPrefab != null)
        {
            Vector3 pos = new Vector3(chargeTargetPos.x, transform.position.y + 0.1f, chargeTargetPos.z);
            currentIndicator = Instantiate(chargeIndicatorPrefab, pos, Quaternion.identity);
            float diameter = chargeImpactRadius * 2;
            currentIndicator.transform.localScale = new Vector3(diameter, 0.1f, diameter);
        }

        yield return new WaitForSeconds(chargePrepTime);

        if (currentIndicator != null) Destroy(currentIndicator);

        currentState = OrcState.Charging;
        anim.SetTrigger("Charge"); // 突進モーション
        agent.enabled = false; // 物理移動のためAgentオフ
    }

    private IEnumerator Routine_Cooldown(float time)
    {
        currentState = OrcState.Cooldown;
        yield return new WaitForSeconds(time);
        currentState = OrcState.Chasing;
    }

    // --- Animation Events (Unityのエディタ側で設定する関数) ---

    /// <summary>
    /// 通常攻撃のアニメーションイベント
    /// 攻撃が当たる瞬間に呼ぶ
    /// </summary>
    public void DealDamageToPlayer()
    {
        if (player == null) return;

        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        if (distance <= attackRange)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle <= attackAngle)
            {
                player.GetComponent<PlayerHP>()?.TakeDamage(attackDamage);
                Debug.Log("オークの通常攻撃ヒット！");
            }
        }
    }

    /// <summary>
    /// 範囲攻撃（スマッシュ）のアニメーションイベント
    /// 武器が地面に叩きつけられた瞬間に呼ぶ
    /// </summary>
    public void ExecuteSmashAttack()
    {
        // エフェクト生成
        if (smashEffectPrefab != null)
        {
            Instantiate(smashEffectPrefab, smashPosition.transform.position, Quaternion.identity);
        }

        // 範囲ダメージ判定 (自分中心の円形範囲)
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= smashRadius)
            {
                player.GetComponent<PlayerHP>()?.TakeDamage(smashDamage);

                // 必要ならここでノックバック処理を追加
                Debug.Log("オークのスマッシュ攻撃ヒット！");
            }
        }
    }

    // オブジェクト破棄時の掃除
    private void OnDestroy()
    {
        if (currentIndicator != null) Destroy(currentIndicator);
    }

    private void OnDrawGizmosSelected()
    {
        // 通常攻撃範囲（扇形）
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Vector3 leftDir = Quaternion.Euler(0, -attackAngle, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, attackAngle, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, leftDir * attackRange);
        Gizmos.DrawRay(transform.position, rightDir * attackRange);

        // 範囲攻撃（スマッシュ）範囲（黄色い円）
        Gizmos.color = new Color(1, 0.92f, 0.016f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, smashRadius);
    }
}