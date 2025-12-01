using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TreasureBox : MonoBehaviour
{
    private Animator anim;
    private bool hasPlayerEntered = false;

    [Header("アイテム設定")]
    // インスペクターからアイテムのプレハブを設定
    public List<GameObject> droppableItems = new List<GameObject>();

    [Header("重複設定")]
    [Tooltip("チェックを入れると、階層をまたいでも同じアイテムが出ないようになります")]
    public bool preventGlobalDuplicates = true;

    [Header("ドロップ設定")]
    public Transform dropPoint;

    // ランタイムで使用するリスト
    private List<GameObject> remainingItems;

    // ★追加: ゲーム起動中、全ての宝箱で共有される「取得済みアイテム」の記憶
    private static HashSet<string> globalObtainedItems = new HashSet<string>();

    // ★追加: ゲームリセット時（タイトルに戻るなど）に呼び出して履歴を消す
    public static void ResetTreasureHistory()
    {
        globalObtainedItems.Clear();
        Debug.Log("宝箱の取得履歴をリセットしました。");
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogError("ChestオブジェクトにAnimatorコンポーネントが見つかりません。");
        }

        if (dropPoint == null)
        {
            dropPoint = transform;
        }

        // --- リストの初期化ロジックを変更 ---
        InitializeDropList();
    }

    private void InitializeDropList()
    {
        // 1. まず元のリストをコピー
        remainingItems = new List<GameObject>(droppableItems);

        // 2. 重複禁止設定がONなら、履歴にあるアイテムを削除する
        if (preventGlobalDuplicates)
        {
            // 名前で照合して削除（アイテム名(Clone)などに対応するため、Prefab名を基準にするのが安全）
            // ここではPrefabのnameそのままで判定します
            remainingItems.RemoveAll(item => globalObtainedItems.Contains(item.name));

            // もしフィルタリングした結果、出すものがなくなってしまった場合
            if (remainingItems.Count == 0 && droppableItems.Count > 0)
            {
                Debug.Log("全てのユニークアイテムを取得済みです。リストをリセットして再抽選可能にします。");
                remainingItems = new List<GameObject>(droppableItems);
            }
        }
    }

    public void OpenChest()
    {
        if (remainingItems.Count > 0)
        {
            anim.SetTrigger("Open");
        }
    }

    // ★★★ アニメーションイベントから呼び出されるメソッド ★★★
    public void SpawnRandomItem()
    {
        if (remainingItems.Count == 0)
        {
            Debug.Log("ドロップ可能なアイテムがありません。");
            return;
        }

        // 1. リストからランダムにアイテムを選択
        int randomIndex = Random.Range(0, remainingItems.Count);
        GameObject itemToDrop = remainingItems[randomIndex];

        // 2. アイテムを生成
        Instantiate(itemToDrop, dropPoint.position, Quaternion.identity);
        Debug.Log(itemToDrop.name + " をドロップしました！");

        // 3. ★追加: グローバル履歴に登録（名前で記憶）
        if (preventGlobalDuplicates)
        {
            if (!globalObtainedItems.Contains(itemToDrop.name))
            {
                globalObtainedItems.Add(itemToDrop.name);
            }
        }

        // 4. この箱のリストから削除（連打防止）
        remainingItems.RemoveAt(randomIndex);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasPlayerEntered)
        {
            hasPlayerEntered = true;
            anim.SetTrigger("open");

            if (remainingItems.Count > 0)
            {
                Debug.Log($"チェストを開けます。候補数: {remainingItems.Count}");
            }
            else
            {
                Debug.Log("チェストにはアイテムがありません（取得済み）。");
            }
        }
    }
}