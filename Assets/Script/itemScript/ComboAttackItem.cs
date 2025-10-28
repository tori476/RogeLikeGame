// BossSummonItem.cs
using UnityEngine;

// BossSummonItem は ItemBase を継承する
public class ComboAttackItem : ItemBase
{
    // Start()とUpdate()は基底クラスのものを利用するため、今回は記述しない。

    // プレイヤーに与える効果（ボス召喚アビリティ付与）を実装
    protected override void ApplyEffect(GameObject playerObject)
    {
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ComboAttackItem();
            Debug.Log("連続攻撃アイテムを取得しました！");
        }
    }
}