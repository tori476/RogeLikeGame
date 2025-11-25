using UnityEngine;

public class FlameDamageCollider : MonoBehaviour
{
    // ダメージ量はボス側のスクリプトから上書きして制御します
    public int damage = 10;

    // 触れている間ずっと呼ばれる（炎のような継続ダメージに最適）
    private void OnParticleCollision(GameObject other)
    {
        // ぶつかった相手がPlayerHPを持っているか確認
        PlayerHP playerHP = other.GetComponent<PlayerHP>();

        if (playerHP != null)
        {
            // プレイヤーが無敵時間中でなければダメージを与える
            // (IsInvincibleチェックを入れないと、無敵中にログが大量に出てしまうため)
            if (!playerHP.IsInvincible())
            {
                playerHP.TakeDamage(damage);
            }
        }
    }
}