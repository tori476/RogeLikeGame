using UnityEngine;

public class NoneItem : ItemBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void ApplyEffect(GameObject playerObject)
    {
        PlayerController playerController = playerObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            //
            playerController.NoneItem();
        }
    }
}
