using UnityEngine;
using System.Collections.Generic;

public class WeaponDamageDealer : MonoBehaviour
{
    [SerializeField]
    private int damage = 25; // この武器の基本ダメージ

    [SerializeField]
    private float hitStopDuration = 0.2f; // ヒットストップの時間

    // 1回の攻撃で同じ敵に複数回ダメージを与えないように、ヒットした敵を記録するリスト
    private List<Collider> hitEnemies = new List<Collider>();

    private Transform playerTransform; // プレイヤー（攻撃者）の位置情報

    // PlayerControllerからプレイヤー情報を初期化してもらう
    public void Initialize(Transform attackerTransform)
    {
        playerTransform = attackerTransform;
    }

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
        // "Enemy" タグを持ち、まだヒットしていない敵なら
        if (other.CompareTag("Enemy") && !hitEnemies.Contains(other))
        {
            // ヒット済みリストに追加
            hitEnemies.Add(other);

            // EnemyAIスクリプトを取得してダメージを与える
            EnemyAI enemy = other.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, playerTransform);
                if (HitStop.Instance != null) //ヒットストップ呼び出し
                {
                    HitStop.Instance.Stop(hitStopDuration);
                }
            }
        }
    }
}