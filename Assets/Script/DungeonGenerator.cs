using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
using System.Linq;

public class DungeonManager : MonoBehaviour
{
    [Header("部屋のプレハブ")]

    public GameObject startRoomPrefab; // スタート部屋専用

    [Header("階層別エンド部屋設定")]
    [Tooltip("階層ごとに使用するEndRoomプレハブのリスト。インデックス0が1階、1が2階...")]
    public GameObject[] endRoomPrefabsByFloor; // 階層別のエンド部屋配列
    public GameObject defaultEndRoomPrefab;    // デフォルトのエンド部屋（階層数を超えた場合用）

    public GameObject treasureRoomPrefab;//宝箱部屋専用
    public GameObject[] normalRoomPrefabs;

    [Header("通路のプレハブ")]
    public GameObject[] corridorPrefabs;

    [Header("壁・扉プレハブ")]
    public GameObject wallPrefab;

    [Header("階段プレハブ")]
    public GameObject stairsPrefab;

    [Header("ダンジョン設定")]
    public int numberOfNormalRooms = 10;
    public int maxPlacementTries = 50; // 最大試行回数

    public List<GameObject> spawnedRooms = new List<GameObject>();
    private List<Bounds> spawnedRoomBounds = new List<Bounds>(); // Boundsをキャッシュするリスト
    public List<Transform> availableConnectors = new List<Transform>();

    // 使用済みコネクターの位置を記録するリスト（壁生成で除外するため）
    private HashSet<Vector3> usedConnectorPositions = new HashSet<Vector3>();

    private NavMeshSurface navMeshSurface; // NavMeshSurfaceへの参照を追加

    void Start()
    {
        navMeshSurface = GetComponent<NavMeshSurface>();
        GenerateDungeon();
        // NavMesh構築
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
    }

    // publicにしてSceneTransitionManagerから呼び出せるようにする
    public void RegenerateDungeon()
    {
        Debug.Log("=== RegenerateDungeon開始 ===");

        // 既存の部屋・階段を全て削除
        foreach (var room in spawnedRooms)
        {
            if (room != null)
            {
                Destroy(room);
            }
        }
        spawnedRooms.Clear();
        spawnedRoomBounds.Clear();
        availableConnectors.Clear();
        usedConnectorPositions.Clear();

        // 階段も全て削除
        foreach (var stairs in GameObject.FindGameObjectsWithTag("Stairs"))
        {
            Destroy(stairs);
        }

        Debug.Log("古いダンジョンを削除しました");

        // NavMeshSurface再取得
        navMeshSurface = GetComponent<NavMeshSurface>();

        // 新しくダンジョン生成
        Debug.Log("新しいダンジョンを生成します...");
        GenerateDungeon();
        Debug.Log("ダンジョン生成完了");

        // NavMesh再構築
        if (navMeshSurface != null)
        {
            Debug.Log("NavMesh再構築中...");
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh再構築完了");
        }

        // NavMeshAgentを全ての部屋で有効化
        foreach (var room in spawnedRooms)
        {
            if (room != null)
            {
                var agents = room.GetComponentsInChildren<NavMeshAgent>(true);
                foreach (var agent in agents)
                {
                    if (agent != null)
                    {
                        agent.enabled = true;
                    }
                }
            }
        }

        // プレイヤーをspawnedRooms[0]（スタート部屋）のNavMesh上に移動
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && spawnedRooms.Count > 0)
        {
            Vector3 startPos = new Vector3(0f, 10f, 0f);
            Debug.Log($"プレイヤーを{startPos}に移動します");

            // CharacterControllerの座標リセット対応
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = startPos;
                controller.enabled = true;
            }
            else
            {
                player.transform.position = startPos;
            }

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.ResetVelocity();
            }

            Debug.Log("プレイヤーの移動完了");
        }
        else
        {
            Debug.LogWarning("プレイヤーが見つからないか、部屋が生成されていません");
        }

        Debug.Log("=== RegenerateDungeon完了 ===");
    }

    private void GenerateDungeon()
    {
        // --- 1. スタート部屋の配置 ---
        if (startRoomPrefab == null)
        {
            Debug.LogError("スタート部屋が設定されていません!");
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

        // --- 3. 宝箱部屋の配置 ---
        if (treasureRoomPrefab != null)
        {
            PlaceTreasureRoom();
        }

        // --- 4. エンド部屋の配置 ---
        GameObject selectedEndRoom = GetEndRoomForCurrentFloor();
        if (selectedEndRoom != null)
        {
            PlaceEndRoom(selectedEndRoom);
        }
        else
        {
            Debug.LogError("使用するエンド部屋プレハブが見つかりません!");
        }

        // 生成結果のログ表示
        if (spawnedRooms.Count < numberOfNormalRooms + 1)
        {
            Debug.LogWarning($"目標の通常部屋数 {numberOfNormalRooms} に届きませんでした。");
        }

        CloseOpenConnectors();
        // NavMesh構築（フレーム遅延不要、即時構築）
        if (navMeshSurface != null)
        {
            StartCoroutine(BuildNavMeshDelayed());
            //navMeshSurface.BuildNavMesh();
        }
    }

    /// <summary>
    /// 現在の階層に応じたEndRoomプレハブを取得
    /// </summary>
    private GameObject GetEndRoomForCurrentFloor()
    {
        // FloorUIControllerから現在の階層を取得
        FloorUIController floorUI = FindFirstObjectByType<FloorUIController>();
        int currentFloor = 1;

        if (floorUI != null)
        {
            currentFloor = floorUI.GetCurrentFloor();
            Debug.Log($"<color=cyan>【EndRoom選択】現在の階層: {currentFloor}階</color>");
        }
        else
        {
            Debug.LogWarning("<color=yellow>FloorUIControllerが見つかりません。1階として扱います。</color>");
        }

        // 配列のインデックスは0から始まるため、階層-1
        int floorIndex = currentFloor - 1;

        // デバッグ情報を詳細に表示
        Debug.Log($"<color=cyan>【EndRoom選択】floorIndex: {floorIndex}, 配列サイズ: {(endRoomPrefabsByFloor != null ? endRoomPrefabsByFloor.Length : 0)}</color>");

        // 階層に対応するプレハブが存在するかチェック
        if (endRoomPrefabsByFloor != null &&
            floorIndex >= 0 &&
            floorIndex < endRoomPrefabsByFloor.Length &&
            endRoomPrefabsByFloor[floorIndex] != null)
        {
            Debug.Log($"<color=green>【EndRoom選択】{currentFloor}階用のEndRoomを使用: {endRoomPrefabsByFloor[floorIndex].name}</color>");
            return endRoomPrefabsByFloor[floorIndex];
        }
        else
        {
            // 配列外または未設定の場合はデフォルトを使用
            string reason = "";
            if (endRoomPrefabsByFloor == null)
                reason = "配列がnull";
            else if (floorIndex < 0)
                reason = $"インデックスが負の値({floorIndex})";
            else if (floorIndex >= endRoomPrefabsByFloor.Length)
                reason = $"インデックス({floorIndex})が配列サイズ({endRoomPrefabsByFloor.Length})を超過";
            else if (endRoomPrefabsByFloor[floorIndex] == null)
                reason = $"Element {floorIndex} がnull";

            Debug.Log($"<color=yellow>【EndRoom選択】{currentFloor}階用の専用EndRoomが使えません({reason})。デフォルトを使用: {(defaultEndRoomPrefab != null ? defaultEndRoomPrefab.name : "null")}</color>");
            return defaultEndRoomPrefab;
        }
    }

    // 部屋の配置を試行するメソッド
    // TryPlaceRoomを通常部屋専用にリネーム
    private bool TryPlaceNormalRoom()
    {
        if (normalRoomPrefabs.Length == 0) return false;
        GameObject roomPrefab = normalRoomPrefabs[Random.Range(0, normalRoomPrefabs.Length)];
        return TryConnectNewItem(roomPrefab);
    }

    private void PlaceTreasureRoom()
    {
        // 利用可能なコネクターのリストをコピーしてシャッフルし、ランダムな接続を試みる
        List<Transform> connectorsToTry = new List<Transform>(availableConnectors);
        // Fisher-Yates shuffleアルゴリズムでリストをシャッフル
        for (int i = 0; i < connectorsToTry.Count; i++)
        {
            Transform temp = connectorsToTry[i];
            int randomIndex = Random.Range(i, connectorsToTry.Count);
            connectorsToTry[i] = connectorsToTry[randomIndex];
            connectorsToTry[randomIndex] = temp;
        }

        bool treasureRoomPlaced = false;
        // シャッフルされたリストをループして配置を試みる
        foreach (var connector in connectorsToTry)
        {
            // 既存の接続メソッドを再利用して配置を試行
            if (TryConnectNewItem(treasureRoomPrefab, connector))
            {
                treasureRoomPlaced = true;
                Debug.Log("宝箱部屋を配置しました。");
                break; // 1つ配置できたらループを抜ける
            }
        }

        if (!treasureRoomPlaced)
        {
            Debug.LogWarning("宝箱部屋を配置できる適切な場所が見つかりませんでした。");
        }
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

    // エンド部屋を配置する専用メソッド（引数でプレハブを受け取るように変更）
    private void PlaceEndRoom(GameObject endRoomPrefab)
    {
        if (endRoomPrefab == null)
        {
            Debug.LogError("EndRoomプレハブがnullです!");
            return;
        }

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

        bool placed = false;
        if (furthestConnector != null)
        {
            // 2. 見つけた場所にエンド部屋を接続してみる
            if (TryConnectNewItem(endRoomPrefab, furthestConnector))
            {
                placed = true;
            }
        }

        // 配置できなかった場合はavailableConnectorsの中から順に配置を試みる
        if (!placed)
        {
            foreach (var connector in availableConnectors)
            {
                if (TryConnectNewItem(endRoomPrefab, connector))
                {
                    placed = true;
                    Debug.Log("エンド部屋を強制配置しました。");
                    break;
                }
            }
        }

        if (!placed)
        {
            Debug.LogError("エンド部屋の配置に完全に失敗しました。コネクターがありません。");
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
        // NavMeshAgentがあれば一時的に無効化（エラー対策）
        var navAgents = newItem.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        foreach (var agent in navAgents) agent.enabled = false;
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


        // NavMesh再構築（部屋追加直後）
        /*if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }*/
        // NavMeshAgentを有効化
        navAgents = newItem.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        foreach (var agent in navAgents)
        {
            agent.enabled = true;
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

    private System.Collections.IEnumerator BuildNavMeshDelayed()
    {
        // 1フレーム待機して、すべてのオブジェクトが確実に配置されるのを待つ
        yield return new WaitForSeconds(0.1f);
        navMeshSurface.BuildNavMesh();
        // NavMeshAgentを有効化
        foreach (var room in spawnedRooms)
        {
            var navAgents = room.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
            foreach (var agent in navAgents)
            {
                agent.enabled = true;
            }
        }
        // NavMesh Bake後にプレイヤーをスタート部屋に移動
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(-0.22f, 1.05f, -9.79f);
        }
    }
}