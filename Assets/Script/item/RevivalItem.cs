using UnityEngine;

public class RevivalItem : ItemBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void ApplyEffect(GameObject playerObject)
    {
        PlayerHP playerHP = playerObject.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.Revival();
        }
    }
}
