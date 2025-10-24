// BossSummonItem.cs
using UnityEngine;

// BossSummonItem は ItemBase を継承する
public class ChargeAttackItem : ItemBase
{
    // Start()とUpdate()は基底クラスのものを利用するため、今回は記述しない。

    // プレイヤーに与える効果（ボス召喚アビリティ付与）を実装
    protected override void ApplyEffect(GameObject playerObject)
    {
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // プレイヤーにボス召喚アビリティを付与する
            playerController.ChargeAttackItem();
            Debug.Log("プレイヤーがボス召喚アイテムを取得しました！");
        }
    }
}