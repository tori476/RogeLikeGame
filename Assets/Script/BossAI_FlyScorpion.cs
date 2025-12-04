using UnityEngine;
using System.Collections;

public class BossAI_FlyScrpion : BossAI
{
    [Header("フライスコーピオンの固有の設定")]
    // 基底クラスの変数を使用するため、SerializeField privateで上書き
    [SerializeField] private new float attackRange = 3.0f;
    [SerializeField] private new float attackCooldown = 2.0f;
    [SerializeField] private new int attackDamage = 20;

    [Header("突進スキル")]
    public float chargeRange = 15.0f;
    public float chargePrepTime = 1.5f;
    public float chargeSpeed = 20.0f;
    public int chargeDamage = 40;
    public float chargeCooldown = 5.0f;
    public float chargeProbability = 0.5f;
    public GameObject chargeIndicatorPrefab;
    public float chargeImpactRadius = 2.5f;

    [Header("弓矢攻撃設定")]
    public GameObject arrowProjectilePrefab;     // 弓矢のプレハブ
    public Transform firePoint;                   // 発射位置
    public float arrowSpeed = 20.0f;             // 弓矢の速度
    public int arrowDamage = 15;                 // 弓矢のダメージ

    [Header("効果音設定")]
    public AudioClip flyingSoundClip;            // スズメバチが飛ぶ音
    public AudioClip arrowShootClip;             // 弓矢を放つ音
    public AudioClip katanaSwingClip;            // 刀を振る音
    [Range(0f, 1f)]
    public float flyingSoundVolume = 0.3f;
    [Range(0f, 1f)]
    public float arrowShootVolume = 0.8f;
    [Range(0f, 1f)]
    public float katanaSwingVolume = 1.0f;

    // 基底クラスのaudioSourceを使用するため、ここでは宣言しない
    private AudioSource flyingAudioSource;       // 飛行音専用

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

        // AudioSourceの設定
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;

        // 飛行音専用のAudioSourceを追加
        flyingAudioSource = gameObject.AddComponent<AudioSource>();
        flyingAudioSource.playOnAwake = false;
        flyingAudioSource.loop = true;
        flyingAudioSource.spatialBlend = 0.5f;

        // firePointが未設定の場合は自分の位置を使用
        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 音声ファイルを動的にロード
        LoadAudioClips();
    }

    // 敵が起動したときに呼ばれる（プレイヤーと会ったとき）
    public override void ActivateEnemy()
    {
        base.ActivateEnemy(); // 基底クラスの処理を実行

        // スズメバチの飛行音を開始
        StartFlyingSound();
    }

    private void LoadAudioClips()
    {
        if (flyingSoundClip == null)
        {
            flyingSoundClip = Resources.Load<AudioClip>("BGM/スズメバチが飛ぶ");
        }
        if (arrowShootClip == null)
        {
            arrowShootClip = Resources.Load<AudioClip>("BGM/弓矢を放つ");
        }
        if (katanaSwingClip == null)
        {
            katanaSwingClip = Resources.Load<AudioClip>("BGM/swinging-a-katana-1");
        }
    }

    private void StartFlyingSound()
    {
        if (flyingAudioSource != null && flyingSoundClip != null)
        {
            flyingAudioSource.clip = flyingSoundClip;
            flyingAudioSource.volume = flyingSoundVolume;
            flyingAudioSource.Play();
            Debug.Log("スズメバチの飛行音を開始しました");
        }
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
        if (dist <= chargeRange && dist > attackRange)
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

        yield return new WaitForSeconds(0.3f); // アニメーション開始を待つ

        // 弓矢を発射
        FireArrow();

        StartCoroutine(Routine_Cooldown(attackCooldown));
    }

    private void FireArrow()
    {
        // 弓矢を放つ音を再生
        if (audioSource != null && arrowShootClip != null)
        {
            audioSource.PlayOneShot(arrowShootClip, arrowShootVolume);
        }

        if (arrowProjectilePrefab == null)
        {
            Debug.LogWarning("弓矢のプレハブが設定されていません！");
            return;
        }

        // 弓矢を生成
        GameObject arrow = Instantiate(arrowProjectilePrefab, firePoint.position, Quaternion.identity);

        // 弓矢のスクリプトを取得して設定
        Projectile projectileScript = arrow.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            Vector3 direction = (player.position - firePoint.position).normalized;
            projectileScript.Initialize(direction, arrowSpeed, arrowDamage, gameObject);
            Debug.Log("弓矢を発射しました！");
        }
        else
        {
            Debug.LogWarning("Projectileコンポーネントが見つかりません！");
            Destroy(arrow);
        }
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

        // 突進開始時に刀を振る音を再生
        if (audioSource != null && katanaSwingClip != null)
        {
            audioSource.PlayOneShot(katanaSwingClip, katanaSwingVolume);
            Debug.Log("刀を振る音を再生しました");
        }

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

        // 飛行音を停止
        if (flyingAudioSource != null && flyingAudioSource.isPlaying)
        {
            flyingAudioSource.Stop();
        }
    }
}