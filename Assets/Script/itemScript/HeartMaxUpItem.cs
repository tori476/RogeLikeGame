using UnityEngine;

public class HeartMaxUpItem : ItemBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void ApplyEffect(GameObject playerObject)
    {
        PlayerHP playerHP = playerObject.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            //最大体力を一つ増やす
            playerHP.MaxHP();
        }
    }
}
