// HeartPickup.cs
using UnityEngine;

public class Heart : MonoBehaviour
{
    [Header("回復設定")]
    public int healAmount = 1; // 回復量

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
        // 接触したのがプレイヤーかどうかをタグで判定
        if (other.CompareTag("Player"))
        {
            // プレイヤーのPlayerHPコンポーネントを取得
            PlayerHP playerHP = other.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                // プレイヤーの体力を回復
                playerHP.Heal(healAmount);

                // 回復させたら自身を消滅させる
                Destroy(gameObject);
            }
        }
    }
}