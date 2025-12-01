using UnityEngine;
using System.Collections.Generic; // ★HashSetを使うために追加

public class FlameSummonDamageCollider : MonoBehaviour
{
    public int damage = 20;

    [HideInInspector]
    public GameObject owner; // 攻撃主

    // ★一度ダメージを与えた敵を記録しておくリスト（セット）
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();

    // パーティクルが何かに当たった時に呼ばれる
    private void OnParticleCollision(GameObject other)
    {
        // 自分自身（召喚ドラゴン）への衝突は無視
        if (owner != null && other == owner) return;

        // 当たった相手が EnemyAI を持っているか確認
        EnemyAI enemy = other.GetComponent<EnemyAI>();

        if (enemy != null)
        {
            // ★すでにこの攻撃でダメージを与えた敵なら、何もしないで終了（除外）
            if (hitEnemies.Contains(enemy.gameObject))
            {
                return;
            }

            // 敵にダメージを与える
            enemy.TakeDamage(damage, transform);

            // ★「ダメージを与えた敵リスト」に追加して、次からは無視するようにする
            hitEnemies.Add(enemy.gameObject);
        }
    }
}