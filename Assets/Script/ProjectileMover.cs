using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    public float speed = 15f;
    public int damage = 20;

    // 回転速度を追加 (1秒間に360度回転)
    public float rotateSpeed = 360f;

    private float hitStopDuration = 0.05f;
    private Transform playerTransform;

    // 発射時の進行方向を記憶するための変数
    private Vector3 moveDirection;

    void Start()
    {
        // 生成された瞬間の「上方向（進行方向）」を記憶する
        // 元のコードが Vector3.down * -speed (つまり上) だったため transform.up を使用
        moveDirection = transform.up;
    }

    void Update()
    {
        // 1. 移動：記憶した方向にワールド座標で移動させる（回転の影響を受けない）
        transform.position += moveDirection * speed * Time.deltaTime;

        // 2. 回転：Z軸（またはX軸）で回転させる
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);

        Destroy(gameObject, 3.0f);
    }

    public void Initialize(Transform targetPlayer)
    {
        playerTransform = targetPlayer;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyAI enemy = other.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, playerTransform);
                if (HitStop.Instance != null)
                {
                    HitStop.Instance.Stop(hitStopDuration);
                }
            }
            other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            // Debug.Log($"敵 {other.name} に {damage} ダメージを与えました！"); // デバッグが不要ならコメントアウト
            Destroy(gameObject);
        }
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }


    }
}