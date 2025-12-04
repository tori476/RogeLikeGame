using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileEnemyAI : EnemyAI
{
    [Header("発射設定")]
    public GameObject projectilePrefab;      // 発射する球のプレハブ
    public Transform firePoint;              // 発射位置（未設定の場合は敵の位置から発射）
    public float detectionRange = 20.0f;     // Playerを検知する距離
    public float projectileSpeed = 15.0f;    // 球の速度
    public float fireRate = 2.0f;            // 発射間隔（秒）
    public int projectileDamage = 0;        // 球のダメージ

    [Header("効果音設定")]
    public AudioClip fireSound;              // 発射時の効果音
    public AudioClip hitSound;               // 命中時の効果音
    [Range(0f, 1f)]
    public float fireSoundVolume = 0.8f;     // 発射音の音量
    [Range(0f, 1f)]
    public float hitSoundVolume = 1.5f;      // 命中音の音量（より大きく）
    // 基底クラスのprotected audioSourceを使用するため、ここでは宣言しない

    private float nextFireTime = 0f;         // 次に発射できる時間
    private bool playerInRange = false;      // Playerが範囲内にいるか

    protected override void Start()
    {
        base.Start();

        // firePointが設定されていない場合は自分の位置を使用
        if (firePoint == null)
        {
            firePoint = transform;
        }

        // AudioSourceコンポーネントを取得、なければ追加
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // AudioSourceの設定を最適化（2D寄りに変更）
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f; // より2D寄りに（聞こえやすく）
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 50f;

        // NavMeshAgentがない場合は警告を出す（意図的にない場合は問題なし）
        if (agent == null)
        {
            Debug.LogWarning($"{gameObject.name} には NavMeshAgent がありません。移動しない敵として動作します。");
        }
    }

    protected override void Update()
    {
        if (!isActivated || player == null)
        {
            return;
        }

        // Playerとの距離を計算
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        playerInRange = distanceToPlayer <= detectionRange;

        // 範囲内にいる場合は発射
        if (playerInRange && Time.time >= nextFireTime && projectilePrefab != null)
        {
            FireProjectile();
            nextFireTime = Time.time + fireRate;
        }

        // NavMeshAgentが存在し、有効な場合のみ追跡行動を実行
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && knockbackCoroutine == null)
        {
            // 攻撃範囲外なら近づく
            if (distanceToPlayer > attackRange)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
            else
            {
                agent.isStopped = true;
            }
        }

        // Playerの方を向く
        if (playerInRange)
        {
            LookAtPlayer();
        }
    }

    private void LookAtPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Y軸の回転を無視
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }

    private void FireProjectile()
    {
        // 発射音を再生
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound, fireSoundVolume);
        }

        // 球を生成
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // 球のスクリプトを取得して設定
        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            // Playerへの方向を計算
            Vector3 direction = (player.position - firePoint.position).normalized;
            projectileScript.Initialize(direction, projectileSpeed, projectileDamage, gameObject);

            // 命中音を設定
            projectileScript.SetHitSound(hitSound);
        }
        else
        {
            Debug.LogWarning("Projectileコンポーネントが見つかりません！");
            Destroy(projectile);
        }

        Debug.Log($"{gameObject.name} が球を発射しました！");
    }

    // Gizmoで検知範囲を表示
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
