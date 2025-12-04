using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// プレイヤーに向かって一直線に突進する敵
/// 壁にぶつかったら3秒停止し、その後再度プレイヤーに向かって突進する
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ChargeEnemyAI : EnemyAI
{
    [Header("突進敵の設定")]
    public float chargeSpeed = 10.0f;        // 突進速度
    public float wallStopDuration = 3.0f;    // 壁にぶつかった後の停止時間
    public LayerMask wallLayer;              // 壁のレイヤー
    public float rotationSpeed = 5.0f;       // プレイヤーの方向への回転速度
    public float chargePreparationTime = 0.5f; // 突進前の準備時間（方向を定める時間）

    [Header("突進敵の効果音設定")]
    public AudioClip chargeMoveSound;        // 移動時の効果音 (hito_ta_aruhashi_tetetete04)
    [Range(0f, 1f)]
    public float chargeMoveSoundVolume = 0.8f;
    public AudioClip chargeAttackSound;      // 攻撃時の効果音 (hito_ge_kamituku02)
    [Range(0f, 1f)]
    public float chargeAttackSoundVolume = 0.8f;
    public AudioClip wallHitSound;           // 壁にぶつかった時の効果音 (se_wall_1)
    [Range(0f, 1f)]
    public float wallHitSoundVolume = 0.8f;

    private bool isCharging = false;          // 突進中かどうか
    private bool isStopped = false;           // 壁にぶつかって停止中かどうか
    private bool isPreparing = false;         // 突進準備中かどうか
    private Vector3 chargeDirection;          // 突進方向（一度決めたら固定）
    private Animator animator;                // アニメーター
    private bool isPlayingMoveSound = false;  // 移動音を再生中かどうか

    protected override void Start()
    {
        base.Start();
        
        // Animatorコンポーネントを取得
        animator = GetComponent<Animator>();
        
        // Rigidbodyの設定
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 回転を固定
        }

        // NavMeshAgentは使用しないので無効化
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 壁レイヤーが設定されていない場合、デフォルトレイヤーを設定
        if (wallLayer == 0)
        {
            wallLayer = LayerMask.GetMask("Default");
        }
    }

    protected override void Update()
    {
        if (!isActivated || player == null || isStopped || knockbackCoroutine != null)
        {
            // 停止中やノックバック中はアニメーションを停止
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
            return;
        }

        // プレイヤーとの距離を計算
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 攻撃範囲内にいて、クールダウンが終わっていれば攻撃
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            AttackPlayer();
            lastAttackTime = Time.time;
            
            // 攻撃中はアニメーションを停止
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
            return;
        }

        // 突進中は方向を変えずに直進し続ける
        if (isCharging)
        {
            ChargeInDirection();
        }
        // 準備中でも突進中でもない場合は、新しい突進を開始
        else if (!isPreparing)
        {
            StartCoroutine(PrepareCharge());
        }
    }

    private IEnumerator PrepareCharge()
    {
        isPreparing = true;

        // 準備時間の間、プレイヤーの方向を向き続ける
        float elapsedTime = 0f;
        while (elapsedTime < chargePreparationTime)
        {
            if (player != null)
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                directionToPlayer.y = 0;

                if (directionToPlayer != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 突進方向を確定（プレイヤーの延長線上に向かって進む）
        if (player != null)
        {
            chargeDirection = (player.position - transform.position).normalized;
            chargeDirection.y = 0;
        }

        // 突進開始
        isPreparing = false;
        isCharging = true;
    }

    private void ChargeInDirection()
    {
        if (rb != null && chargeDirection != Vector3.zero)
        {
            // 確定した方向に一定速度で直進
            Vector3 velocity = chargeDirection * chargeSpeed;
            velocity.y = rb.linearVelocity.y; // Y軸の速度は維持（重力の影響を受ける）
            rb.linearVelocity = velocity;

            // 突進中のアニメーション
            if (animator != null)
            {
                animator.SetFloat("Speed", chargeSpeed);
            }

            // 移動音を再生（ループ再生）
            if (audioSource != null && chargeMoveSound != null && !isPlayingMoveSound)
            {
                audioSource.clip = chargeMoveSound;
                audioSource.volume = chargeMoveSoundVolume;
                audioSource.loop = true;
                audioSource.Play();
                isPlayingMoveSound = true;
            }
        }
    }

    private void AttackPlayer()
    {
        Debug.Log($"{gameObject.name} がプレイヤーを攻撃！攻撃力: {attackDamage}");

        // 攻撃効果音を再生（攻撃専用の音）
        if (audioSource != null && chargeAttackSound != null)
        {
            // ループ音を停止
            StopMoveSound();
            // 攻撃音を再生
            audioSource.PlayOneShot(chargeAttackSound, chargeAttackSoundVolume);
        }

        PlayerHP playerHP = player.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(attackDamage, true);
            Debug.Log($"{gameObject.name} がプレイヤーに {attackDamage} のダメージを与えた！");
        }

        // 攻撃後は一時停止
        StartCoroutine(StopAfterAttack());
    }

    private IEnumerator StopAfterAttack()
    {
        isStopped = true;
        isCharging = false;

        // 移動音を停止
        StopMoveSound();

        // 速度をゼロに
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        yield return new WaitForSeconds(attackCooldown);

        isStopped = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 壁にぶつかったかチェック
        if (((1 << collision.gameObject.layer) & wallLayer) != 0)
        {
            Debug.Log($"{gameObject.name} が壁にぶつかりました！");
            
            // 壁にぶつかった時の効果音を再生
            if (audioSource != null && wallHitSound != null)
            {
                // ループ音を停止
                StopMoveSound();
                // 壁衝突音を再生
                audioSource.PlayOneShot(wallHitSound, wallHitSoundVolume);
            }
            
            // 既に停止中でなければ停止処理を開始
            if (!isStopped && isCharging)
            {
                StartCoroutine(StopAfterWallHit());
            }
        }
    }

    private IEnumerator StopAfterWallHit()
    {
        isStopped = true;
        isCharging = false;

        // 移動音を停止
        StopMoveSound();

        // 速度をゼロに
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        // アニメーションを停止
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }

        Debug.Log($"{gameObject.name} が {wallStopDuration} 秒間停止します");

        // 指定時間待機
        yield return new WaitForSeconds(wallStopDuration);

        Debug.Log($"{gameObject.name} が再び動き出します");

        // 突進方向をリセット（次の突進のため）
        chargeDirection = Vector3.zero;
        isStopped = false;
    }

    private void StopMoveSound()
    {
        if (audioSource != null && isPlayingMoveSound)
        {
            audioSource.loop = false;
            audioSource.Stop();
            isPlayingMoveSound = false;
        }
    }

    public override void ActivateEnemy()
    {
        if (isActivated) return;

        isActivated = true;
        Debug.Log(this.gameObject.name + " が起動しました！");
    }

    // デバッグ用：Gizmoで突進方向を表示
    private void OnDrawGizmos()
    {
        if (isCharging && chargeDirection != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, chargeDirection * 3f);
        }
    }
}
