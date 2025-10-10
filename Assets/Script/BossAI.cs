using UnityEngine;
using UnityEngine.AI; // NavMeshAgentを使うために必要
using System.Collections;
using System;

public class BossAI : EnemyAI
{
    [Header("ボス専用設定")]
    public float attackRange = 3.0f;    // 攻撃を開始するプレイヤーとの距離
    public float attackCooldown = 2.0f; // 攻撃後の待ち時間（秒）
    public int attackDamage = 20;       // ボスの攻撃力

    [Header("突進攻撃の設定")]
    public float chargeAttackRange = 15.0f;     // 突進攻撃を考慮し始める距離
    public float chargePreparationTime = 1.5f;  // 突進前の溜め時間（秒）
    public float chargeSpeed = 20.0f;           // 突進の速度
    public int chargeDamage = 40;               // 突進の攻撃力
    public float chargeAttackCooldown = 5.0f;   // 突進後の待ち時間（秒）
    public float chargeAttackProbability = 0.5f; // 突進攻撃を選択する確率 (0.0 ~ 1.0)
    public GameObject chargeIndicatorPrefab;    // 突進先の地面に表示するインジケーターのプレハブ
    public float chargeImpactRadius = 2.5f;     // 突進がヒットした際のダメージ範囲

    // ボスの状態を定義する
    private enum BossState
    {
        Chasing,    // 追跡中
        Attacking,  // 攻撃中
        PreparingCharge,// 突進の溜め中
        Charging,       // 突進中
        Cooldown    // クールダウン中
    }
    private BossState currentState; // 現在の状態を保持する変数
    private Animator anim; // アニメーションを制御するため

    public event Action<BossAI> OnBossDied;

    private Vector3 chargeTargetPosition;       // 突進の目標地点
    private GameObject currentChargeIndicator;  // 生成したインジケーターを保持する変数


    protected override void Start()
    {
        // まず親のStart()を呼び出して、基本的な初期化（agentやplayerの取得）を行わせる
        base.Start();

        anim = GetComponent<Animator>();

        // 初期状態をChasingに設定
        currentState = BossState.Chasing;
    }

    protected override void Update()
    {
        if (!isActivated || player == null)
        {
            return;
        }

        // 突進中以外は常にプレイヤーの方向を向く
        if (currentState == BossState.Chasing)
        {
            LookAtPlayer();
        }
        // ステートマシン
        switch (currentState)
        {
            case BossState.Chasing:
                HandleChasingState();
                break;
            case BossState.Attacking:
                // HandleAttackingStateはコルーチンで管理するため、Updateでの処理は不要
                break;
            case BossState.PreparingCharge:
                // HandlePreparingChargeStateもコルーチンで管理
                break;
            case BossState.Charging:
                HandleChargingState();
                break;
            case BossState.Cooldown:
                // HandleCooldownStateはコルーチンで管理
                break;
        }
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    // --- 追跡状態の処理 ---
    private void HandleChasingState()
    {
        // NavMeshAgentが有効でなければ何もしない
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // プレイヤーに向かって移動
        agent.isStopped = false;
        agent.SetDestination(player.position);

        // プレイヤーとの距離を計算
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 距離が攻撃範囲内に入ったら、攻撃状態に移行
        if (distanceToPlayer <= chargeAttackRange)
        {
            // 確率で突進攻撃を選択
            if (UnityEngine.Random.value < chargeAttackProbability)
            {
                StartCoroutine(PrepareChargeCoroutine());
                return; // 新しい状態に移行したので追跡処理を抜ける
            }
        }

        // 通常攻撃の射程内か？
        if (distanceToPlayer <= attackRange)
        {
            StartCoroutine(AttackCoroutine());
        }
    }

    // --- 通常攻撃の処理（コルーチン） ---
    private IEnumerator AttackCoroutine()
    {
        currentState = BossState.Attacking;
        agent.isStopped = true;

        // 攻撃アニメーションを再生
        anim.SetTrigger("Attack");

        // アニメーションの長さに合わせて待つか、固定時間待つ
        // ここではすぐにクールダウンへ移行
        yield return null; // 1フレーム待ってからクールダウンへ

        StartCoroutine(CooldownCoroutine(attackCooldown));
    }

    // --- 突進準備状態の処理（コルーチン） ---
    private IEnumerator PrepareChargeCoroutine()
    {
        currentState = BossState.PreparingCharge;
        agent.isStopped = true;

        // 溜めアニメーションを再生
        anim.SetTrigger("PrepareCharge");

        // プレイヤーの現在の位置を目標地点として設定
        chargeTargetPosition = player.position;

        // ダメージサークルを生成
        if (chargeIndicatorPrefab != null)
        {
            // Y座標を地面に合わせる（必要に応じて調整）
            Vector3 indicatorPosition = new Vector3(chargeTargetPosition.x, transform.position.y, chargeTargetPosition.z);
            currentChargeIndicator = Instantiate(chargeIndicatorPrefab, indicatorPosition, Quaternion.identity);
            // サークルの大きさをダメージ範囲に合わせる
            currentChargeIndicator.transform.localScale = new Vector3(chargeImpactRadius * 2, 0.1f, chargeImpactRadius * 2);
        }

        // 溜め時間だけ待機
        yield return new WaitForSeconds(chargePreparationTime);

        // 溜めが終わったらダメージサークルを削除
        if (currentChargeIndicator != null)
        {
            Destroy(currentChargeIndicator);
        }

        // 突進状態へ移行
        currentState = BossState.Charging;
        // 突進アニメーションを再生
        anim.SetTrigger("Charge");
        // NavMeshAgentを無効化して物理的な移動に切り替える
        if (agent.isActiveAndEnabled)
        {
            agent.enabled = false;
        }
    }

    // --- 突進状態の処理 ---
    private void HandleChargingState()
    {
        // 目標地点に向かって直接移動させる
        transform.position = Vector3.MoveTowards(transform.position, chargeTargetPosition, chargeSpeed * Time.deltaTime);

        // 目標地点に十分に近づいたら突進終了
        if (Vector3.Distance(transform.position, chargeTargetPosition) < 0.5f)
        {
            // 突進のダメージ処理
            DealChargeDamage();

            // NavMeshAgentを再度有効化
            agent.enabled = true;

            // クールダウンへ移行
            StartCoroutine(CooldownCoroutine(chargeAttackCooldown));
        }
    }

    // --- クールダウン状態の処理（コルーチン） ---
    private IEnumerator CooldownCoroutine(float duration)
    {
        currentState = BossState.Cooldown;
        // 指定された時間だけ待機
        yield return new WaitForSeconds(duration);
        // 追跡状態に戻す
        currentState = BossState.Chasing;
    }

    // --- アニメーションイベントから呼び出されるメソッド（通常攻撃用） ---
    public void DealDamageToPlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange)
        {
            PlayerHP playerHP = player.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(attackDamage);
                Debug.Log("ボスがプレイヤーに " + attackDamage + " のダメージを与えた！");
            }
        }
    }

    // --- 突進攻撃のダメージを与えるメソッド ---
    private void DealChargeDamage()
    {
        // プレイヤーがダメージ範囲内にいるかチェック
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= chargeImpactRadius)
        {
            PlayerHP playerHP = player.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(chargeDamage);
                Debug.Log("ボスの突進がヒット！プレイヤーに " + chargeDamage + " のダメージ！");
            }
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
        OnBossDied?.Invoke(this);
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        // もしインジケーター（突進の目印）が生成されていて、まだシーンに残っている場合に
        if (currentChargeIndicator != null)
        {
            // インジケーターも一緒に破棄する
            Destroy(currentChargeIndicator);
        }
    }
}
