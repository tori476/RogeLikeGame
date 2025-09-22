using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DungeonManager : MonoBehaviour
{
    [Header("部屋のプレハブ")]

    public GameObject startRoomPrefab; // スタート部屋専用
    public GameObject endRoomPrefab;   // エンド部屋（ボス部屋）専用
    public GameObject[] normalRoomPrefabs;

    [Header("通路のプレハブ")]
    public GameObject[] corridorPrefabs;

    [Header("壁・扉プレハブ")]
    public GameObject wallPrefab;

    [Header("ダンジョン設定")]
    public int numberOfNormalRooms = 10;
    public int maxPlacementTries = 50; // 最大試行回数

    private List<GameObject> spawnedRooms = new List<GameObject>();
    private List<Bounds> spawnedRoomBounds = new List<Bounds>(); // Boundsをキャッシュするリスト
    public List<Transform> availableConnectors = new List<Transform>();

    // 使用済みコネクターの位置を記録するリスト（壁生成で除外するため）
    private HashSet<Vector3> usedConnectorPositions = new HashSet<Vector3>();

    void Start()
    {
        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        // --- 1. スタート部屋の配置 ---
        if (startRoomPrefab == null)
        {
            Debug.LogError("スタート部屋が設定されていません！");
            return;
        }
        GameObject startRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity);
        spawnedRooms.Add(startRoom);
        spawnedRoomBounds.Add(CalculateBounds(startRoom));
        AddConnectorsToList(startRoom);

        // --- 2. 通常部屋の配置 ---
        int placementTries = 0;
        while (spawnedRooms.Count < numberOfNormalRooms + 1 && placementTries < maxPlacementTries)
        {
            // 配置に成功したらTryPlaceNormalRoom()はtrueを返す
            if (TryPlaceNormalRoom())
            {
                placementTries = 0; // 成功でリセット
            }
            else
            {
                placementTries++; // 失敗でカウント
            }
        }

        // --- 3. エンド部屋の配置 ---
        if (endRoomPrefab != null)
        {
            PlaceEndRoom();
        }

        // 生成結果のログ表示
        if (spawnedRooms.Count < numberOfNormalRooms + 1)
        {
            Debug.LogWarning($"目標の通常部屋数 {numberOfNormalRooms} に届きませんでした。");
        }

        CloseOpenConnectors();
    }

    // 部屋の配置を試行するメソッド
    // TryPlaceRoomを通常部屋専用にリネーム
    private bool TryPlaceNormalRoom()
    {
        if (normalRoomPrefabs.Length == 0) return false;
        GameObject roomPrefab = normalRoomPrefabs[Random.Range(0, normalRoomPrefabs.Length)];
        return TryConnectNewItem(roomPrefab);
    }

    void AddConnectorsToList(GameObject room)
    {
        foreach (Transform connector in GetAllConnectors(room))
        {
            availableConnectors.Add(connector);
        }
    }

    Transform[] GetAllConnectors(GameObject room)
    {
        return room.GetComponentsInChildren<Transform>().Where(t => t.name == "Connector").ToArray();
    }

    // エンド部屋を配置する専用メソッド
    private void PlaceEndRoom()
    {
        // 1. スタート地点から最も遠いコネクターを探す
        Transform furthestConnector = null;
        float maxDistance = 0f;
        foreach (var connector in availableConnectors)
        {
            float currentDistance = Vector3.Distance(Vector3.zero, connector.position);
            if (currentDistance > maxDistance)
            {
                maxDistance = currentDistance;
                furthestConnector = connector;
            }
        }

        if (furthestConnector == null)
        {
            Debug.LogError("エンド部屋を接続できるコネクターが見つかりませんでした。");
            return;
        }

        // 2. 見つけた場所にエンド部屋を接続してみる
        if (!TryConnectNewItem(endRoomPrefab, furthestConnector))
        {
            Debug.LogWarning("エンド部屋の配置に失敗しました。");
        }
    }

    // TryConnectNewItemメソッドを丸ごと置き換える
    private bool TryConnectNewItem(GameObject itemPrefab, Transform specificConnector = null)
    {
        if (availableConnectors.Count == 0 || itemPrefab == null) return false;

        int connectorIndex = -1;
        Transform existingConnector;

        // 接続元のコネクターを決定（引数で指定されていなければランダム）
        if (specificConnector != null)
        {
            existingConnector = specificConnector;
        }
        else
        {
            connectorIndex = Random.Range(0, availableConnectors.Count);
            existingConnector = availableConnectors[connectorIndex];
        }

        // --- ステージ1: 新しい部屋の配置 ---

        // 1. 新しい部屋を生成し、そのコネクターをランダムに選ぶ
        GameObject newItem = Instantiate(itemPrefab);
        Transform[] newItemConnectors = GetAllConnectors(newItem);
        if (newItemConnectors.Length == 0) // コネクターがないプレハブはエラー
        {
            Debug.LogError($"プレハブ '{itemPrefab.name}' に Connector がありません。");
            Destroy(newItem);
            return false;
        }
        Transform newItemConnector = newItemConnectors[Random.Range(0, newItemConnectors.Length)];

        // 2. 部屋同士のコネクターを合わせて仮配置
        AlignObject(newItem, newItemConnector, existingConnector);

        // --- ステージ2: 衝突判定 ---

        // 3. 衝突をチェック
        if (CheckCollision(newItem))
        {
            Destroy(newItem);
            return false;
        }

        // --- ステージ3: 配置の確定 ---

        // 4. 部屋をリストに追加
        spawnedRooms.Add(newItem);
        spawnedRoomBounds.Add(CalculateBounds(newItem));

        // 5. 使用済みコネクターの位置を記録（小数点以下を丸める）
        Vector3 existingPos = new Vector3(
            Mathf.Round(existingConnector.position.x * 100f) / 100f,
            Mathf.Round(existingConnector.position.y * 100f) / 100f,
            Mathf.Round(existingConnector.position.z * 100f) / 100f
        );
        Vector3 newPos = new Vector3(
            Mathf.Round(newItemConnector.position.x * 100f) / 100f,
            Mathf.Round(newItemConnector.position.y * 100f) / 100f,
            Mathf.Round(newItemConnector.position.z * 100f) / 100f
        );

        usedConnectorPositions.Add(existingPos);
        usedConnectorPositions.Add(newPos);

        // 6. コネクターリストを更新
        availableConnectors.Remove(existingConnector); // 接続済みの古いコネクターを削除
        foreach (var connector in newItemConnectors)
        {
            if (connector != newItemConnector)
            {
                availableConnectors.Add(connector); // 新しい部屋の未使用コネクターを追加
            }
        }
        return true;
    }

    private Bounds CalculateBounds(GameObject room)
    {
        var combinedBounds = new Bounds(room.transform.position, Vector3.zero);
        var renderers = room.GetComponentsInChildren<Renderer>();
        foreach (var render in renderers)
        {
            // 最初のBoundsを初期化
            if (combinedBounds.extents == Vector3.zero)
            {
                combinedBounds = render.bounds;
            }
            else
            {
                combinedBounds.Encapsulate(render.bounds);
            }
        }
        return combinedBounds;
    }

    // 衝突判定ロジックを別メソッドに切り出す
    private bool CheckCollision(GameObject item)
    {
        Bounds itemBounds = CalculateBounds(item);
        itemBounds.Expand(-2.0f);
        foreach (var existingBounds in spawnedRoomBounds)
        {
            if (existingBounds.Intersects(itemBounds))
            {
                return true; // 衝突した
            }
        }
        return false; // 衝突なし
    }

    // オブジェクトをコネクターに合わせて配置するロジックを別メソッドに切り出す
    private void AlignObject(GameObject objectToAlign, Transform connectorToAlign, Transform targetConnector)
    {
        Quaternion targetRotation = Quaternion.LookRotation(-targetConnector.forward, Vector3.up);
        objectToAlign.transform.rotation = targetRotation * Quaternion.Inverse(connectorToAlign.localRotation);
        objectToAlign.transform.position = targetConnector.position - (connectorToAlign.position - objectToAlign.transform.position);
        objectToAlign.transform.position += targetConnector.forward * 0.01f;
    }

    // 修正版：すべてのコネクターから使用済みを除外して壁を生成
    private void CloseOpenConnectors()
    {
        if (wallPrefab == null) return; // 壁プレハブがなければ何もしない

        // 全ての部屋の全てのコネクターを取得
        List<Transform> allConnectors = new List<Transform>();
        foreach (var room in spawnedRooms)
        {
            allConnectors.AddRange(GetAllConnectors(room));
        }

        Debug.Log($"全コネクター数: {allConnectors.Count}, 使用済みコネクター位置数: {usedConnectorPositions.Count}");

        // 使用済みでないコネクターに壁を生成
        int wallCount = 0;
        foreach (var connector in allConnectors)
        {
            if (connector != null)
            {
                // コネクターの位置を丸める
                Vector3 roundedPos = new Vector3(
                    Mathf.Round(connector.position.x * 100f) / 100f,
                    Mathf.Round(connector.position.y * 100f) / 100f,
                    Mathf.Round(connector.position.z * 100f) / 100f
                );

                if (!usedConnectorPositions.Contains(roundedPos))
                {
                    // コネクターの位置と向きに合わせて壁を生成する
                    Instantiate(wallPrefab, new Vector3(connector.position.x, connector.position.y + 2, connector.position.z), connector.rotation, connector);
                    wallCount++;
                    Debug.Log($"壁を生成: {connector.name} at {connector.position}");
                }
                else
                {
                    Debug.Log($"使用済みコネクター: {connector.name} at {connector.position} (rounded: {roundedPos})");
                }
            }
        }
        Debug.Log($"生成された壁の数: {wallCount}");
    }
}