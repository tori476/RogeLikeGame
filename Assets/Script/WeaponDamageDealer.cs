using UnityEngine;
using System.Collections.Generic;

public class WeaponDamageDealer : MonoBehaviour
{
    [SerializeField]
    private int damage = 25; // この武器の基本ダメージ

    // 1回の攻撃で同じ敵に複数回ダメージを与えないように、ヒットした敵を記録するリスト
    private List<Collider> hitEnemies = new List<Collider>();

    // 攻撃判定が有効になったときに呼ばれる
    public void StartDealDamage()
    {
        // 攻撃開始時にリストをクリアする
        hitEnemies.Clear();
    }

    // ダメージ量を外部から設定・変更するためのメソッド（溜め攻撃などで使用）
    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. まず、何かに触れたら必ずログを出す
        Debug.Log(gameObject.name + " が " + other.name + " に接触しました！");
        // "Enemy" タグを持ち、まだヒットしていない敵なら
        if (other.CompareTag("Enemy") && !hitEnemies.Contains(other))
        {
            // ヒット済みリストに追加
            hitEnemies.Add(other);

            // EnemyAIスクリプトを取得してダメージを与える
            EnemyAI enemy = other.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                Debug.Log(other.name + " に " + damage + " ダメージを与えます！");
                enemy.TakeDamage(damage);
            }
        }
    }
}