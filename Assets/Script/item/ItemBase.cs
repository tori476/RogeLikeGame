// ItemBase.cs
using UnityEngine;

// すべてのアイテムの基底クラス
public abstract class ItemBase : MonoBehaviour
{
    protected Rigidbody rb;

    // アイテムがドロップされた際の初期化処理
    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            // Rigidbodyが必須であるため、無い場合は警告を出して処理を中断
            Debug.LogWarning(gameObject.name + " に Rigidbody がアタッチされていません！");
            return;
        }

        // ドロップ時のランダムな初期力を加える（共通処理）
        // Y軸の力は固定で、XとZにランダムな力を加える
        rb.AddForce(Random.Range(-200, 200), 50, Random.Range(-200, 200));
    }

    // アイテムの回転処理
    protected virtual void Update()
    {
        // 軽い回転アニメーション（共通処理）
        transform.Rotate(0, 0.1f, 0);
    }

    // プレイヤーと接触したときの処理
    private void OnTriggerEnter(Collider other)
    {
        // 接触したのがプレイヤーかどうかをタグで判定（共通処理）
        if (other.CompareTag("Player"))
        {
            // アイテム取得時の効果音を再生
            if (GameBGMPlayer.Instance != null)
            {
                GameBGMPlayer.Instance.PlayItemGetSound();
            }

            // 継承先のクラスで個別の効果処理を実行
            ApplyEffect(other.gameObject);

            // 効果を適用したら自身を消滅させる（共通処理）
            Destroy(gameObject);
        }
    }

    // 継承先のアイテムが、プレイヤーに与える効果を記述するための抽象メソッド
    // abstractにすることで、継承クラスでの実装を強制する
    protected abstract void ApplyEffect(GameObject playerObject);
}