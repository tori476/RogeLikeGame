using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Listの操作（ランダム選択、削除）に必要
public class TreasureBox : MonoBehaviour
{
    private Animator anim;
    private bool hasPlayerEntered = false;

    [Header("アイテム設定")]
    // インスペクターからアイテムのプレハブ（ItemBaseを継承したもの）を複数設定するリスト
    public List<GameObject> droppableItems = new List<GameObject>();

    [Header("ドロップ設定")]
    public Transform dropPoint; // アイテムが出現する位置（子オブジェクトの空のTransformを推奨）

    // ドロップ可能なアイテムのリストを保持・管理するためのランタイムリスト
    private List<GameObject> remainingItems;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("ChestオブジェクトにAnimatorコンポーネントが見つかりません。");
        }

        if (dropPoint == null)
        {
            Debug.LogWarning("Drop Pointが設定されていません。チェスト自身の位置をドロップ地点として使用します。");
            dropPoint = transform;
        }

        // 初期アイテムリストをコピーしてランタイムリストを作成
        remainingItems = new List<GameObject>(droppableItems);
    }

    // プレイヤーがチェストに近づいたときや操作キーを押したときに呼ばれる想定のメソッド
    public void OpenChest()
    {
        // アイテムが残っているかチェック
        if (remainingItems.Count > 0)
        {
            // チェストを開けるアニメーションを再生
            anim.SetTrigger("Open");
            Debug.Log("チェストを開けます。残りドロップ回数: " + remainingItems.Count);
        }
        else
        {
            Debug.Log("チェストにはもうアイテムが残っていません。");
            // アイテムがない場合の待機アニメーションなどを再生しても良い
        }
    }

    // ★★★ アニメーションイベントから呼び出されるメソッド ★★★
    // アニメーションクリップ内の、アイテムが出現するタイミングのフレームにイベントを設定してください。
    public void SpawnRandomItem()
    {
        // 残っているアイテムがなければドロップ処理を中断
        if (remainingItems.Count == 0)
        {
            Debug.Log("ドロップ可能なアイテムが残っていません。");
            return;
        }

        // 1. リストからランダムにアイテムを選択
        int randomIndex = Random.Range(0, remainingItems.Count);
        GameObject itemToDrop = remainingItems[randomIndex];

        // 2. アイテムを生成（ItemBaseのStart()で自動的に飛び出す）
        Instantiate(itemToDrop, dropPoint.position, Quaternion.identity);
        Debug.Log(itemToDrop.name + " をドロップしました！");

        // 3. 一度ドロップしたアイテムをリストから削除（二度と出ないようにする）
        remainingItems.RemoveAt(randomIndex);
        Debug.Log("アイテムをリストから削除しました。残りアイテム数: " + remainingItems.Count);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasPlayerEntered)
        {
            hasPlayerEntered = true;
            anim.SetTrigger("open");
            // アイテムが残っているかチェック
            if (remainingItems.Count > 0)
            {
                Debug.Log("チェストを開けます。残りドロップ回数: " + remainingItems.Count);
            }
            else
            {
                Debug.Log("チェストにはもうアイテムが残っていません。");
                // アイテムがない場合の待機アニメーションなどを再生しても良い
            }
        }
    }
}
