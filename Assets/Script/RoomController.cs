using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RoomController : MonoBehaviour
{
    [Header("部屋のロック設定")]
    // 閉じ込めるために出入り口に生成するプレハブ（壁や扉など）
    public GameObject doorPrefab;
    // Inspectorから部屋の中にあるライトを登録するための配列
    public GameObject[] roomLights;

    // --- プライベート変数 ---
    private List<EnemyAI> enemiesInRoom = new List<EnemyAI>();
    private List<GameObject> spawnedDoors = new List<GameObject>();
    private Transform[] connectors;
    private bool isCleared = false; // この部屋の敵が全滅したかどうかのフラグ

    private bool hasPlayerEntered = false;

    void Awake()
    {
        // 1. 自分の子オブジェクトから全てのEnemyAIコンポーネントを取得し、リストに登録
        enemiesInRoom.AddRange(GetComponentsInChildren<EnemyAI>());

        // 2. 部屋の敵が0なら、最初からクリア済み扱いにする
        if (enemiesInRoom.Count == 0)
        {
            isCleared = true;
            return; // 敵がいないので以下の処理は不要
        }

        // 3. 部屋にいる全ての敵の「死亡イベント」に、自分のメソッドを登録する
        foreach (var enemy in enemiesInRoom)
        {
            // enemyが死んだら、OnEnemyDefeated()メソッドが呼ばれるようになる
            enemy.OnEnemyDied += OnEnemyDefeated;
        }

        // 4. 出入り口となるConnectorを取得しておく
        connectors = GetComponentsInChildren<Transform>().Where(t => t.name == "Connector").ToArray();

        Debug.Log($"部屋 '{this.gameObject.name}' には {enemiesInRoom.Count} 体の敵がいます。");
    }

    // このメソッドは、Trigger内に他のColliderが入ってきた時に自動で呼び出される
    private void OnTriggerEnter(Collider other)
    {
        // 入ってきたオブジェクトのタグが "Player" だったら
        if (other.CompareTag("Player") && !hasPlayerEntered)
        {
            // 一度入ったのでフラグを立てる（二重起動を防ぐため）
            hasPlayerEntered = true;
            // ライトを全てつける
            SetLights(true);
            //敵を起動
            ActivateAllEnemies();
            // まだクリアしておらず、敵がいる部屋ならロックする
            if (!isCleared && enemiesInRoom.Count > 0)
            {
                LockRoom();
            }
        }
    }

    private void ActivateAllEnemies()
    {
        Debug.Log($"部屋 '{this.gameObject.name}' の敵を起動します。");
        foreach (var enemy in enemiesInRoom)
        {
            if (enemy != null)
            {
                // 各EnemyAIスクリプトのActivateEnemyメソッドを呼び出す
                enemy.ActivateEnemy();
            }
        }
    }

    // 敵が一体倒されるたびに呼び出されるメソッド
    private void OnEnemyDefeated(EnemyAI defeatedEnemy)
    {
        // 引数で受け取った敵がリストに存在すれば、それを直接削除する
        if (defeatedEnemy != null && enemiesInRoom.Contains(defeatedEnemy))
        {
            enemiesInRoom.Remove(defeatedEnemy);
        }

        // これでカウントが正確になる
        Debug.Log($"敵が一体倒されました。残り: {enemiesInRoom.Count} 体");

        // 残りの敵が0になったら
        if (enemiesInRoom.Count <= 0)
        {
            isCleared = true;
            Debug.Log($"部屋 '{this.gameObject.name}' の敵が全滅しました！");
            UnlockRoom();
        }
    }

    // 部屋の出入り口を塞ぐメソッド
    private void LockRoom()
    {
        if (doorPrefab == null)
        {
            Debug.LogWarning("Door Prefabが設定されていません。部屋をロックできません。");
            return;
        }

        Debug.Log($"部屋 '{this.gameObject.name}' をロックします。");
        foreach (var connector in connectors)
        {
            // Connectorの位置と向きに合わせて扉を生成
            GameObject door = Instantiate(doorPrefab, new Vector3(connector.position.x, connector.position.y + 2, connector.position.z), connector.rotation, this.transform);
            spawnedDoors.Add(door);
        }
    }

    // 部屋の出入り口を開放するメソッド
    private void UnlockRoom()
    {
        Debug.Log($"部屋 '{this.gameObject.name}' をアンロックします。");
        foreach (var door in spawnedDoors)
        {
            Destroy(door);
        }
        spawnedDoors.Clear();
    }

    // ライトのON/OFFを切り替えるための補助メソッド
    private void SetLights(bool state)
    {
        foreach (var lightObject in roomLights)
        {
            if (lightObject != null)
            {
                lightObject.SetActive(state);
            }
        }
    }
}