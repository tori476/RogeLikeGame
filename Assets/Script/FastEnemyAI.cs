using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 通常の敵より移動速度が速い敵
/// 見た目と攻撃パターンは通常の敵と同じ
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FastEnemyAI : EnemyAI
{
    [Header("高速敵の設定")]
    public float chaseSpeed = 8.0f;      // 通常の移動速度
    public float sprintSpeed = 15.0f;    // スプリント速度
    public float sprintDistance = 10.0f; // プレイヤーがこの距離以内ならスプリント

    protected override void Update()
    {
        if (!isActivated || player == null || agent == null)
        {
            return;
        }

        // NavMeshAgent が有効で、NavMesh 上にいるかチェック
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"{gameObject.name}: NavMeshAgent が無効または NavMesh 上にいません");
            return;
        }

        // ノックバック中は移動しない
        if (knockbackCoroutine != null)
        {
            agent.isStopped = true;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 攻撃範囲内なら攻撃
        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            AttackPlayer();
            lastAttackTime = Time.time;
            agent.isStopped = true;
            return;
        }

        // 追跡：速度を調整しながらプレイヤーに接近
        agent.isStopped = false;
        agent.SetDestination(player.position);

        // プレイヤーが近ければスプリント、遠ければ通常速度
        if (distanceToPlayer <= sprintDistance)
        {
            agent.speed = sprintSpeed;
        }
        else
        {
            agent.speed = chaseSpeed;
        }
    }

    private void AttackPlayer()
    {
        Debug.Log($"{gameObject.name} がプレイヤーを攻撃！");

        PlayerHP playerHP = player.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(attackDamage, true); // EnemyAIからの攻撃であることを示す
        }
    }
}
// 変更なし - EnemyAIの改善が自動的に適用されます
