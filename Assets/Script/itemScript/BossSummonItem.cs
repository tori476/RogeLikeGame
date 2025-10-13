// BossSummonItem.cs
using UnityEngine;

public class BossSummonItem : MonoBehaviour
{

    Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.AddForce(Random.Range(-1000, 1000), 3000, Random.Range(-1000, 1000));
    }

    private void Update()
    {
        transform.Rotate(0, 0.1f, 0);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // プレイヤーにボス召喚アビリティを付与する
                playerController.GrantBossSummonAbility();

                // アイテムを消滅させる
                Destroy(gameObject);
            }
        }
    }
}
