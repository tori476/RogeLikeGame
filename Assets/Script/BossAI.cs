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

    // ボスの状態を定義する
    private enum BossState
    {
        Chasing,    // 追跡中
        Attacking,  // 攻撃中
        Cooldown    // クールダウン中
    }
    private BossState currentState; // 現在の状態を保持する変数
    private Animator anim; // アニメーションを制御するため

    public event Action<BossAI> OnBossDied;

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
        // ステートマシン
        switch (currentState)
        {
            case BossState.Chasing:
                HandleChasingState();
                break;
            case BossState.Attacking:
                HandleAttackingState();
                break;
            case BossState.Cooldown:
                HandleCooldownState();
                break;
        }
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
        if (distanceToPlayer <= attackRange)
        {
            currentState = BossState.Attacking;
        }
    }

    // --- 攻撃状態の処理 ---
    private void HandleAttackingState()
    {
        // 移動を停止
        agent.isStopped = true;

        // プレイヤーの方向を向く
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        // 攻撃アニメーションを再生（トリガー名はAnimatorで設定したものに合わせる）
        anim.SetTrigger("Attack");

        // 攻撃が終わったらクールダウンへ移行するため、コルーチンを開始
        // "Attack"状態は一度だけ実行されれば良いので、すぐにCooldownへ移行
        currentState = BossState.Cooldown;
        StartCoroutine(CooldownCoroutine());
    }

    // --- クールダウン状態の処理 ---
    private void HandleCooldownState()
    {
        // この状態では何もしない。コルーチンが時間を計測している。
    }

    // --- クールダウン時間を待ってから追跡状態に戻すコルーチン ---
    private IEnumerator CooldownCoroutine()
    {
        // 攻撃後の待ち時間だけ待機
        yield return new WaitForSeconds(attackCooldown);

        // 待機後、追跡状態に戻す
        currentState = BossState.Chasing;
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

    // BossAI.cs に追加
    // アニメーションイベントから呼び出されるメソッド
    public void DealDamageToPlayer()
    {
        // プレイヤーとの距離を再度チェック（攻撃モーション中に避けられる可能性を考慮）
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange)
        {
            // プレイヤーのHPコンポーネントを取得してダメージを与える
            PlayerHP playerHP = player.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                playerHP.TakeDamage(attackDamage);
                Debug.Log("ボスがプレイヤーに " + attackDamage + " のダメージを与えた！");
            }
        }
    }

    // 死亡時の処理を行うメソッド
    private void Die()
    {
        Debug.Log(gameObject.name + " は倒された！");
        OnBossDied?.Invoke(this);
        // このゲームオブジェクトをシーンから削除する
        Destroy(gameObject);
    }
}
