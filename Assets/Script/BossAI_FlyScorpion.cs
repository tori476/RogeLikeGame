using UnityEngine;
using System.Collections;

public class BossAI_FlyScrpion : BossAI
{
    [Header("フライスコーピオンの固有の設定")]
    public float attackRange = 3.0f;
    public float attackCooldown = 2.0f;
    public int attackDamage = 20;

    [Header("突進スキル")]
    public float chargeRange = 15.0f;
    public float chargePrepTime = 1.5f;
    public float chargeSpeed = 20.0f;
    public int chargeDamage = 40;
    public float chargeCooldown = 5.0f;
    public float chargeProbability = 0.5f;
    public GameObject chargeIndicatorPrefab;
    public float chargeImpactRadius = 2.5f;

    // ステートマシンはこのクラス専用にする
    private enum FlyScrpionState { Chasing, Attacking, PreparingCharge, Charging, Cooldown }
    private FlyScrpionState currentState = FlyScrpionState.Chasing;

    private Animator anim;
    private Vector3 chargeTargetPos;
    private GameObject currentIndicator;

    protected override void Start()
    {
        base.Start(); // BossAI (と EnemyAI) のStartを呼ぶ
        anim = GetComponent<Animator>();
        currentState = FlyScrpionState.Chasing;
    }

    // BossAI で定義した HandleBehavior をここで上書きして、独自の行動を書く
    protected override void HandleBehavior()
    {
        // 常にプレイヤーの方を向く（突進中以外）
        if (currentState == FlyScrpionState.Chasing)
        {
            LookAtPlayer();
        }

        switch (currentState)
        {
            case FlyScrpionState.Chasing:
                ProcessChasing();
                break;
            case FlyScrpionState.Charging:
                ProcessCharging();
                break;
                // 他のステートはコルーチンで制御しているのでここでは何もしない
        }
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

        // 突進の判定
        if (dist <= chargeRange)
        {
            if (UnityEngine.Random.value < chargeProbability)
            {
                StartCoroutine(Routine_PrepareCharge());
                return;
            }
        }

        // 通常攻撃の判定
        if (dist <= attackRange)
        {
            StartCoroutine(Routine_Attack());
        }
    }

    private void ProcessCharging()
    {
        // NavMeshを使わず直接移動
        transform.position = Vector3.MoveTowards(transform.position, chargeTargetPos, chargeSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, chargeTargetPos) < 0.5f)
        {
            // ヒット判定
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer <= chargeImpactRadius)
            {
                player.GetComponent<PlayerHP>()?.TakeDamage(chargeDamage);
            }

            agent.enabled = true; // 復帰
            StartCoroutine(Routine_Cooldown(chargeCooldown));
        }
    }

    // --- コルーチン群 ---

    private IEnumerator Routine_Attack()
    {
        currentState = FlyScrpionState.Attacking;
        agent.isStopped = true;
        anim.SetTrigger("Attack");

        yield return null;
        // アニメーションイベントでダメージ判定を入れる想定(DealDamageToPlayerなど)

        StartCoroutine(Routine_Cooldown(attackCooldown));
    }

    private IEnumerator Routine_PrepareCharge()
    {
        currentState = FlyScrpionState.PreparingCharge;
        agent.isStopped = true;
        anim.SetTrigger("PrepareCharge");

        chargeTargetPos = player.position;

        // インジケーター表示
        if (chargeIndicatorPrefab != null)
        {
            // 表示位置
            Vector3 pos = new Vector3(chargeTargetPos.x, transform.position.y + 0.1f, chargeTargetPos.z);

            currentIndicator = Instantiate(chargeIndicatorPrefab, pos, Quaternion.identity);

            float diameter = chargeImpactRadius * 2;
            currentIndicator.transform.localScale = new Vector3(diameter, 0.1f, diameter);
        }

        yield return new WaitForSeconds(chargePrepTime);

        if (currentIndicator != null) Destroy(currentIndicator);

        currentState = FlyScrpionState.Charging;
        anim.SetTrigger("Charge");
        agent.enabled = false; // 物理移動のためオフに
    }

    private IEnumerator Routine_Cooldown(float time)
    {
        currentState = FlyScrpionState.Cooldown;
        yield return new WaitForSeconds(time);
        currentState = FlyScrpionState.Chasing;
    }

    // アニメーションイベントから呼ばれる
    public void DealDamageToPlayer()
    {
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            player.GetComponent<PlayerHP>()?.TakeDamage(attackDamage);
        }
    }

    // オブジェクト破棄時の掃除
    private void OnDestroy()
    {
        if (currentIndicator != null) Destroy(currentIndicator);
    }
}