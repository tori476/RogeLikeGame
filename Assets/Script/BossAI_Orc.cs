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

    [Header("サウンド設定")]
    public AudioClip encounterSound; // beast-monster-cry-2
    public AudioClip normalAttackSound; // 打撃6
    public AudioClip smashSound1; // 発砲1
    public AudioClip smashSound2; // ani_fa_mon10
    public AudioClip deathSound; // se_monster_oak4
    public AudioClip randomCrySound; // ani_fa_mon04
    public AudioClip footstepSound; // ani_fa_mon01 (移動時の足音)
    public float randomCryInterval = 5.0f; // ランダム鳴き声の間隔（秒）
    public float randomCryChance = 0.3f; // ランダム鳴き声の発生確率

    // ステートマシン
    private enum OrcState { Chasing, Attacking, PreparingCharge, Charging, Cooldown }
    private OrcState currentState = OrcState.Chasing;

    private Animator anim;
    private Vector3 chargeTargetPos;
    private GameObject currentIndicator;
    private bool hasPlayedEncounterSound = false;
    private float nextRandomCryTime;
    private AudioSource secondaryAudioSource; // 重要な音用の追加AudioSource
    private AudioSource footstepAudioSource; // 足音専用のAudioSource
    private bool isMoving = false; // 移動中かどうかのフラグ

    protected override void Start()
    {
        base.Start();
        anim = GetComponent<Animator>();
        currentState = OrcState.Chasing;

        // AudioSourceは親クラスで既に設定されている
        audioSource.spatialBlend = 0f; // 2Dサウンド（0 = 2D, 1 = 3D）

        // 重要な音用の追加AudioSourceを作成
        secondaryAudioSource = gameObject.AddComponent<AudioSource>();
        secondaryAudioSource.spatialBlend = 0f;
        secondaryAudioSource.playOnAwake = false;

        // 足音専用のAudioSourceを作成
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.spatialBlend = 0f;
        footstepAudioSource.playOnAwake = false;

        // 次のランダム鳴き声の時間を設定
        nextRandomCryTime = Time.time + randomCryInterval;
    }

    /// <summary>
    /// ボスが起動された瞬間（プレイヤーと出会った瞬間）に呼ばれる
    /// </summary>
    public override void ActivateEnemy()
    {
        base.ActivateEnemy();

        // 出会った瞬間の咆哮音を再生
        if (!hasPlayedEncounterSound)
        {
            PlaySound(encounterSound);
            hasPlayedEncounterSound = true;
        }
    }

    protected override void HandleBehavior()
    {
        UpdateMoveAnimation();

        // ちょくちょくランダムな鳴き声を再生
        if (Time.time >= nextRandomCryTime && Random.value < randomCryChance)
        {
            PlaySound(randomCrySound);
            nextRandomCryTime = Time.time + randomCryInterval;
        }

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

        // 足音の再生
        if (currentSpeed > 0.1f && !isMoving)
        {
            isMoving = true;
            StartCoroutine(PlayFootstepSound());
        }
        else if (currentSpeed <= 0.1f && isMoving)
        {
            isMoving = false;
            footstepAudioSource.Stop();
        }
    }

    private IEnumerator PlayFootstepSound()
    {
        while (isMoving)
        {
            if (footstepSound != null)
            {
                footstepAudioSource.PlayOneShot(footstepSound);
            }
            yield return new WaitForSeconds(0.5f); // 足音の間隔を調整
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

        // ani_fa_mon10をアニメーション開始と同時に再生（2秒後に終わる）
        if (smashSound2 != null)
        {
            Debug.Log("ani_fa_mon10: アニメーション開始時に再生開始");
            PlayImportantSound(smashSound2);
        }

        anim.SetTrigger("SmashAttack"); // 範囲攻撃用アニメーションTrigger

        // アニメーションイベント ExecuteSmashAttack() が呼ばれるのを待つ
        // 硬直時間は長めに設定（ani_fa_mon10の再生時間2秒を考慮）
        yield return new WaitForSeconds(2.0f);

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
        // 通常攻撃音を再生
        PlaySound(normalAttackSound);

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
        // ani_fa_mon10を即座に再生開始（2秒の音声が叩きつけ終了時に終わるように）
        if (smashSound2 != null)
        {
            Debug.Log("ani_fa_mon10: 即座に再生開始（2秒後に叩きつけ終了）");
            PlayImportantSound(smashSound2);
        }

        // 発砲1を即座に再生（叩きつけの衝撃音）
        PlaySound(smashSound1);
        Debug.Log("ExecuteSmashAttack: 発砲1を再生");

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

    /// <summary>
    /// 指定した秒数後にサウンドを再生するコルーチン
    /// </summary>
    private IEnumerator PlayDelayedSound(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"PlayDelayedSound: {clip.name} を再生");
        PlaySound(clip); // 通常のAudioSourceで再生
    }

    /// <summary>
    /// 指定した秒数後に重要なサウンドを再生するコルーチン
    /// </summary>
    private IEnumerator PlayDelayedImportantSound(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"PlayDelayedImportantSound: {clip.name} を再生");
        PlayImportantSound(clip); // 専用AudioSourceで再生
    }

    /// <summary>
    /// ボスが死亡した時に呼ばれる
    /// </summary>
    protected override void Die()
    {
        // 死亡音を再生
        PlaySound(deathSound);
        GameClearManager.Instance.TriggerGameClear();

        base.Die();
    }

    /// <summary>
    /// サウンドを再生するヘルパーメソッド
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 重要なサウンドを再生するヘルパーメソッド（他の音と重ならないように専用AudioSourceで再生）
    /// </summary>
    private void PlayImportantSound(AudioClip clip)
    {
        if (clip != null && secondaryAudioSource != null)
        {
            Debug.Log($"PlayImportantSound: {clip.name} を再生開始");
            // 現在再生中の音を停止してから新しい音を再生
            if (secondaryAudioSource.isPlaying)
            {
                secondaryAudioSource.Stop();
            }
            secondaryAudioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"PlayImportantSound: clip={clip}, secondaryAudioSource={secondaryAudioSource}");
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