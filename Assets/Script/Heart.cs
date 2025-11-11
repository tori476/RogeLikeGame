// Heart.cs
using UnityEngine;

// Heart は ItemBase を継承する
public class Heart : ItemBase
{
    [Header("回復設定")]
    public int healAmount = 1; // 回復量

    // Start()とUpdate()は基底クラスのものを利用するため、今回は記述しない。
    // 必要に応じて override して拡張・上書きすることも可能。

    // プレイヤーに与える効果（回復）を実装
    protected override void ApplyEffect(GameObject playerObject)
    {
        // プレイヤーのPlayerHPコンポーネントを取得
        PlayerHP playerHP = playerObject.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            // プレイヤーの体力を回復
            playerHP.Heal(healAmount);
            Debug.Log("プレイヤーがハートを取得し、体力 " + healAmount + " を回復しました！");
        }
    }
}