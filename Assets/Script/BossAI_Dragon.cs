using UnityEngine;
using System.Collections;

public class BossAI_Dragon : BossAI
{
    [Header("ドラゴンの固有の設定")]
    public float flameAttackCooldown = 3.0f;

    public int flameDamage = 10;

    [Header("炎攻撃のエフェクト設定（追加部分）")]
    public GameObject flameEffectPrefab; // 炎のパーティクル（ループ再生するもの）
    public Transform flameSpawnPoint;    // 頭（口元）に作成した空オブジェクト
    private GameObject currentFlameInstance; // 生成中の炎インスタンス保持用

    [Header("攻撃範囲の角度制限")]
    [Range(0, 180)]
    public float attackAngle = 45.0f; // 前方中心から左右に何度まで許容するか（45なら合計90度の扇形）

    [Header("突進スキル")]
    public float chargeRange = 15.0f;
    public float chargePrepTime = 1.5f;
    public float chargeSpeed = 20.0f;
    public int chargeDamage = 40;
    public float chargeCooldown = 5.0f;
    public float chargeProbability = 0.5f;
    public GameObject chargeIndicatorPrefab;
    public float chargeImpactRadius = 2.5f;

    [Header("音声設定")]
    public AudioClip encounterSound;    // se_dragon02 (対面時)
    public AudioClip flameBreathSound;  // dragon-breathing-fire-364475 (炎攻撃)
    public AudioClip normalAttackSound; // hito_ge_kamituku02 (通常攻撃)
    public AudioClip deathSound;        // se_dragon09_death (死亡時)
    public AudioClip footstepSound;     // 怪獣の足音 (移動時)

    // 基底クラスのaudioSourceを使用するため、ここでは宣言しない
    private AudioSource flameAudioSource; // 炎攻撃専用のAudioSource
    private AudioSource footstepAudioSource; // 足音専用のAudioSource
    private bool hasPlayedEncounterSound = false; // 対面音を一度だけ再生するフラグ
    private bool isMoving = false; // 移動中かどうかのフラグ

    // ステートマシンはこのクラス専用にする
    private enum DragonState { Chasing, Attacking, PreparingCharge, Charging, Cooldown }
    private DragonState currentState = DragonState.Chasing;

    private Animator anim;
    private Vector3 chargeTargetPos;
    private GameObject currentIndicator;

    protected override void Start()
    {
        base.Start(); // BossAI (と EnemyAI) のStartを呼ぶ
        anim = GetComponent<Animator>();
        currentState = DragonState.Chasing;

        // 炎攻撃専用のAudioSourceを追加
        flameAudioSource = gameObject.AddComponent<AudioSource>();
        // flameAudioSource.spatialBlend = 1.0f; // 3Dサウンドに設定
        flameAudioSource.maxDistance = 50000000000f;   // 聞こえる最大距離
        flameAudioSource.loop = true;         // ループ再生を有効に
        flameAudioSource.volume = 30000000000000.0f;       // 音量をさらに大きく設定

        // 足音専用のAudioSourceを追加
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.loop = true; // ループ再生を有効に
        footstepAudioSource.volume = 1.0f; // 音量を適切に設定
        footstepAudioSource.clip = footstepSound; // 足音クリップを設定
    }

    // BossAI で定義した HandleBehavior をここで上書きして、独自の行動を書く
    protected override void HandleBehavior()
    {

        UpdateMoveAnimation();

        // 常にプレイヤーの方を向く（突進中以外）
        if (currentState == DragonState.Chasing)
        {
            LookAtPlayer();
        }

        switch (currentState)
        {
            case DragonState.Chasing:
                ProcessChasing();
                break;
            case DragonState.Charging:
                ProcessCharging();
                break;
                // 他のステートはコルーチンで制御しているのでここでは何もしない
        }
    }

    private void UpdateMoveAnimation()
    {
        if (anim == null) return;

        float currentSpeed = 0f;

        // NavMeshAgentが有効、かつNavMesh上にいる場合のみ速度を取得
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            // 現在の速度（ベクトルの長さ）を取得
            currentSpeed = agent.velocity.magnitude;
        }

        // Animatorのパラメータ "Speed" に値をセット
        // 0.1f は減衰値（DampTime）で、急激な変化を滑らかにします
        anim.SetFloat("Speed", currentSpeed, 0.1f, Time.deltaTime);

        // 足音の再生制御
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

        // 対面時の効果音（一度だけ再生）
        if (!hasPlayedEncounterSound && dist <= attackRange * 2f)
        {
            PlaySound(encounterSound);
            hasPlayedEncounterSound = true;
        }

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
            Vector3 dirToPlayer = player.position - transform.position;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            // 射程内 かつ 指定角度内（正面）にいる時だけ攻撃モーションを始める
            if (angle <= attackAngle)
            {
                int rand = Random.Range(0, 2);
                switch (rand)
                {
                    case 0:
                        StartCoroutine(Routine_Attack());
                        break;
                    case 1:
                        StartCoroutine(Routine_Flame_Attack());
                        break;
                }
            }
            else
            {
                // 近くにいるけど横や後ろにいる場合
                // 素早く振り向く処理を入れると自然になります
                // LookAtPlayer(); は常に呼ばれているので、自然と正面を向くまで待つことになります
            }
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
        currentState = DragonState.Attacking;
        agent.isStopped = true;
        anim.SetTrigger("Attack");

        // 通常攻撃の効果音を再生
        PlaySound(normalAttackSound);

        yield return null;
        // アニメーションイベントでダメージ判定を入れる想定(DealDamageToPlayerなど)

        StartCoroutine(Routine_Cooldown(attackCooldown));
    }

    private IEnumerator Routine_Flame_Attack()
    {
        currentState = DragonState.Attacking;
        agent.isStopped = true;
        anim.SetTrigger("FlameAttack");

        // 炎攻撃の効果音を再生
        if (flameAudioSource != null && flameBreathSound != null)
        {
            flameAudioSource.clip = flameBreathSound;
            flameAudioSource.Play();
        }

        yield return null;
        // アニメーションイベントでダメージ判定を入れる想定(DealDamageToPlayerなど)

        StartCoroutine(Routine_Cooldown(flameAttackCooldown));
    }

    private IEnumerator Routine_PrepareCharge()
    {
        currentState = DragonState.PreparingCharge;
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

        currentState = DragonState.Charging;
        anim.SetTrigger("Charge");
        agent.enabled = false; // 物理移動のためオフに
    }

    private IEnumerator Routine_Cooldown(float time)
    {
        currentState = DragonState.Cooldown;
        yield return new WaitForSeconds(time);
        currentState = DragonState.Chasing;
    }

    // アニメーションイベントから呼ばれる
    public void DealDamageToPlayer()
    {
        if (player == null) return;

        // 1. 距離の判定
        Vector3 dirToPlayer = player.position - transform.position;
        float distance = dirToPlayer.magnitude;

        if (distance <= attackRange)
        {
            // 2. 角度の判定 (扇形)
            // ボスの正面と、プレイヤーへの方向ベクトルの角度差を取得
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            // 指定した角度以内（例: 正面から45度以内）ならヒット
            if (angle <= attackAngle)
            {
                player.GetComponent<PlayerHP>()?.TakeDamage(attackDamage);
                Debug.Log("前方攻撃ヒット！");
            }
            else
            {
                Debug.Log("距離は近いが、正面にいないためミス");
            }
        }
    }


    /// アニメーションイベント：炎攻撃の「開始」タイミングで呼ぶ
    public void StartFlameBreath()
    {
        if (flameEffectPrefab != null && flameSpawnPoint != null)
        {
            // すでに炎が出ていれば一度消す（念のため）
            if (currentFlameInstance != null) Destroy(currentFlameInstance);

            // 炎を生成
            currentFlameInstance = Instantiate(flameEffectPrefab, flameSpawnPoint.position, flameSpawnPoint.rotation);

            // 頭（口）の動きに追従させるために親子関係を設定
            currentFlameInstance.transform.SetParent(flameSpawnPoint);

            FlameDamageCollider flameScript = currentFlameInstance.GetComponent<FlameDamageCollider>();
            if (flameScript != null)
            {
                flameScript.damage = flameDamage;
            }
        }
    }
    /// アニメーションイベント：炎攻撃の「終了」タイミングで呼ぶ
    public void EndFlameBreath()
    {
        if (currentFlameInstance != null)
        {
            // 1. いきなりDestroyせず、まずはParticleSystemを取得
            ParticleSystem ps = currentFlameInstance.GetComponent<ParticleSystem>();

            if (ps != null)
            {
                // 2. 「これ以上新しい炎は出さない」という命令を出す
                // すでに出ている炎は寿命が来るまでそのまま残ります
                ps.Stop();
            }

            // 3. (重要) 炎の残像が頭の動きについてくると不自然なので、親子関係を解除する
            // これにより、ドラゴンの頭が動いても、吐き終わった煙はその場に留まります
            currentFlameInstance.transform.SetParent(null);

            // 4. 残りの炎が完全に消えるくらいの時間（例えば3秒後）にオブジェクトを消す
            Destroy(currentFlameInstance, 3.0f);

            // 参照を切る
            currentFlameInstance = null;
        }

        // 炎攻撃の効果音を停止
        if (flameAudioSource != null)
        {
            flameAudioSource.Stop();
        }
    }

    // オブジェクト破棄時の掃除
    private void OnDestroy()
    {
        if (currentIndicator != null) Destroy(currentIndicator);
        if (currentFlameInstance != null) Destroy(currentFlameInstance);
    }
    private void OnDrawGizmosSelected()
    {
        // 攻撃範囲（赤色）
        Gizmos.color = new Color(1, 0, 0, 0.3f);

        // 円を描く代わりに扇形の両端の線を描画
        Vector3 leftDir = Quaternion.Euler(0, -attackAngle, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, attackAngle, 0) * transform.forward;

        Gizmos.DrawRay(transform.position, leftDir * attackRange);
        Gizmos.DrawRay(transform.position, rightDir * attackRange);

        // 前方の線
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);
    }

    /// <summary>
    /// 効果音を再生するヘルパーメソッド
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 死亡時に呼ばれるメソッド（BossAIまたはEnemyAIで死亡処理がある場合はそこから呼ぶ）
    /// </summary>
    public void PlayDeathSound()
    {
        PlaySound(deathSound);
    }

    // 親クラスでDieメソッドがあればオーバーライド
    protected override void Die()
    {
        // 死亡音を再生してから親クラスのDie()を呼ぶ
        if (audioSource != null && deathSound != null)
        {
            // AudioSource.PlayClipAtPointを使用すると、オブジェクトが破壊されても音が鳴り続ける
            AudioSource.PlayClipAtPoint(deathSound, transform.position, 3.0f);
        }

        base.Die();
    }
}
