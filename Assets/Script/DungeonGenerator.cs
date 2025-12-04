using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

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
        StartCoroutine(CO_RegenerateDungeon());
        //GenerateDungeon();
        // NavMesh構築
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
    }

    // publicにしてSceneTransitionManagerから呼び出せるようにする
    public void RegenerateDungeon()
    {
        // ★変更：実処理をコルーチンに委譲して開始する
        StartCoroutine(CO_RegenerateDungeon());
    }

    // ★追加：コルーチン化した再生成処理
    private IEnumerator CO_RegenerateDungeon()
    {
        Debug.Log("=== RegenerateDungeon開始 ===");

        // --- 既存の部屋・階段削除 ---
        foreach (var room in spawnedRooms)
        {
            if (room != null) Destroy(room);
        }
        spawnedRooms.Clear();
        spawnedRoomBounds.Clear();
        availableConnectors.Clear();
        usedConnectorPositions.Clear();

        foreach (var stairs in GameObject.FindGameObjectsWithTag("Stairs"))
        {
            Destroy(stairs);
        }

        Debug.Log("古いダンジョンを削除指示完了。削除反映を1フレーム待ちます...");

        // ★重要：Destroyが完全に反映されるまで1フレーム待つ
        yield return null;

        // NavMeshSurface再取得
        navMeshSurface = GetComponent<NavMeshSurface>();

        // --- 新ダンジョン生成 ---
        Debug.Log("新しいダンジョンを生成します...");
        GenerateDungeon();
        Debug.Log("ダンジョン生成完了。物理演算/座標反映を1フレーム待ちます...");

        // ★重要：Instantiate後の座標確定やCollider更新を待つため1フレーム待機
        yield return null;

        // --- NavMesh再構築 ---
        if (navMeshSurface != null)
        {
            Debug.Log("NavMesh再構築中...");
            navMeshSurface.BuildNavMesh(); // ここで確定したジオメトリに対してベイク
            Debug.Log("NavMesh再構築完了");
        }

        // --- NavMeshAgent有効化 ---
        foreach (var room in spawnedRooms)
        {
            if (room != null)
            {
                var agents = room.GetComponentsInChildren<NavMeshAgent>(true);
                foreach (var agent in agents)
                {
                    if (agent != null) agent.enabled = true;
                }
            }
        }

        // --- プレイヤー移動 ---
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && spawnedRooms.Count > 0)
        {
            // スタート部屋の位置へ移動（Y軸など微調整が必要な場合は調整してください）
            Vector3 startPos = spawnedRooms[0].transform.position + new Vector3(0f, 1f, 0f);

            // CharacterController干渉対策
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = startPos;
                yield return null; // ★念のためここでも1フレーム待つと安全
                controller.enabled = true;
            }
            else
            {
                player.transform.position = startPos;
            }

            // 速度リセットなど
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.ResetVelocity();
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

        // ===== デバッグ情報 =====
        Debug.Log($"<color=cyan>【ダンジョン生成完了】合計部屋数: {spawnedRooms.Count}</color>");
        for (int i = 0; i < spawnedRooms.Count; i++)
        {
            var room = spawnedRooms[i];
            if (room != null)
            {
                Debug.Log($"  部屋{i}: {room.name} at {room.transform.position}, Active: {room.activeSelf}, Layer: {LayerMask.LayerToName(room.layer)}");

                // レンダラーの状態確認
                var renderers = room.GetComponentsInChildren<Renderer>();
                int visibleCount = 0;
                foreach (var r in renderers)
                {
                    if (r.enabled) visibleCount++;
                }
                Debug.Log($"    Renderers: {visibleCount}/{renderers.Length} 有効");
            }
        }

        // カメラの位置と向きを確認
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"<color=yellow>【カメラ情報】位置: {mainCam.transform.position}, 向き: {mainCam.transform.forward}, Far Clip: {mainCam.farClipPlane}</color>");
        }
        else
        {
            Debug.LogWarning("<color=red>メインカメラが見つかりません!</color>");
        }

        CloseOpenConnectors();
        // NavMesh構築（フレーム遅延不要、即時構築）
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
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
            // 2. 見つけた場所にエンド部屋を接続してみる（EndRoom側の全コネクターを試す）
            if (TryConnectNewItemToSpecificConnector_AllNewConnectors(endRoomPrefab, furthestConnector))
            {
                placed = true;
                Debug.Log("エンド部屋を最遠コネクターに配置しました。");
            }
        }

        // 配置できなかった場合はavailableConnectorsの中から順に（シャッフルで）配置を試みる
        if (!placed)
        {
            var connectorsToTry = new List<Transform>(availableConnectors);
            // Fisher-Yates shuffle
            for (int i = 0; i < connectorsToTry.Count; i++)
            {
                int r = Random.Range(i, connectorsToTry.Count);
                (connectorsToTry[i], connectorsToTry[r]) = (connectorsToTry[r], connectorsToTry[i]);
            }

            foreach (var connector in connectorsToTry)
            {
                if (TryConnectNewItemToSpecificConnector_AllNewConnectors(endRoomPrefab, connector))
                {
                    placed = true;
                    Debug.Log("エンド部屋を強制配置しました。（全コネクター試行成功）");
                    break;
                }
            }

            // 直接接続が全滅した場合、通路経由での接続を試みる
            if (!placed && corridorPrefabs != null && corridorPrefabs.Length > 0)
            {
                foreach (var connector in connectorsToTry)
                {
                    if (TryPlaceEndRoomViaCorridor(endRoomPrefab, connector))
                    {
                        placed = true;
                        Debug.Log("エンド部屋を通路経由で配置しました。");
                        break;
                    }
                }
            }

            // 最終手段: 衝突チェックを緩和して強制配置
            if (!placed && connectorsToTry.Count > 0)
            {
                Debug.LogWarning("通常配置が全て失敗。衝突チェック緩和で強制配置を試みます...");
                foreach (var connector in connectorsToTry)
                {
                    if (ForcePlace_Relaxed(endRoomPrefab, connector))
                    {
                        placed = true;
                        Debug.Log("<color=yellow>エンド部屋を衝突緩和モードで強制配置しました。</color>");
                        break;
                    }
                }
            }
        }

        if (!placed)
        {
            Debug.LogError("エンド部屋の配置に完全に失敗しました。適切なコネクターの組み合わせが見つかりませんでした。");
        }
    }

    // 指定した既存コネクターに対して、新規プレハブ側の全コネクターを順に試す
    private bool TryConnectNewItemToSpecificConnector_AllNewConnectors(GameObject itemPrefab, Transform existingConnector)
    {
        if (existingConnector == null || itemPrefab == null) return false;

        // 新しい部屋を生成（1インスタンスを全コネクターで位置合わせして試す）
        GameObject newItem = Instantiate(itemPrefab);
        // NavMeshAgentは一時無効化
        var navAgents = newItem.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        foreach (var agent in navAgents) agent.enabled = false;

        var newItemConnectors = GetAllConnectors(newItem);
        if (newItemConnectors.Length == 0)
        {
            Debug.LogError($"プレハブ '{itemPrefab.name}' に Connector がありません。");
            Destroy(newItem);
            return false;
        }

        // シャッフルして衝突しにくい向きを積極的に探す
        for (int i = 0; i < newItemConnectors.Length; i++)
        {
            int r = Random.Range(i, newItemConnectors.Length);
            (newItemConnectors[i], newItemConnectors[r]) = (newItemConnectors[r], newItemConnectors[i]);
        }

        // 各コネクターで試行
        foreach (var newItemConnector in newItemConnectors)
        {
            // 位置合わせ
            AlignObject(newItem, newItemConnector, existingConnector);

            // 衝突チェック
            if (CheckCollision(newItem))
            {
                continue; // 次のコネクターで再試行
            }

            // 成功: リスト更新、使用済みコネクター記録、NavMesh再構築、有効化
            spawnedRooms.Add(newItem);
            spawnedRoomBounds.Add(CalculateBounds(newItem));

            // 位置丸め
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

            // コネクターリスト更新
            availableConnectors.Remove(existingConnector);
            foreach (var c in newItemConnectors)
            {
                if (c != newItemConnector)
                {
                    availableConnectors.Add(c);
                }
            }

            // NavMesh再構築
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
            // NavMeshAgentを有効化
            foreach (var agent in navAgents) agent.enabled = true;

            return true;
        }

        // 全コネクターで失敗した場合
        Destroy(newItem);
        return false;
    }

    // 直接接続に失敗した際のフォールバック：通路を介してEndRoomを接続
    private bool TryPlaceEndRoomViaCorridor(GameObject endRoomPrefab, Transform existingConnector)
    {
        if (existingConnector == null || endRoomPrefab == null) return false;
        if (corridorPrefabs == null || corridorPrefabs.Length == 0) return false;

        foreach (var corridorPrefab in corridorPrefabs)
        {
            if (corridorPrefab == null) continue;

            GameObject corridor = Instantiate(corridorPrefab);
            var corridorAgents = corridor.GetComponentsInChildren<NavMeshAgent>(true);
            foreach (var a in corridorAgents) a.enabled = false;

            var corridorConnectors = GetAllConnectors(corridor);
            if (corridorConnectors.Length < 2)
            {
                Destroy(corridor);
                continue;
            }

            bool placed = false;

            // Corridorの全コネクターを、既存側接続候補として試す
            foreach (var attachConnector in corridorConnectors)
            {
                // アラインしてから衝突チェック
                AlignObject(corridor, attachConnector, existingConnector);
                if (CheckCollision(corridor))
                {
                    continue;
                }

                // 一時登録（EndRoom配置が失敗したらロールバック）
                spawnedRooms.Add(corridor);
                var corridorBounds = CalculateBounds(corridor);
                spawnedRoomBounds.Add(corridorBounds);

                // 反対側コネクター（EndRoom側）を決定
                Transform nextConnector = corridorConnectors.First(c => c != attachConnector);

                // 反対側にEndRoomを接続
                bool endPlaced = TryConnectNewItemToSpecificConnector_AllNewConnectors(endRoomPrefab, nextConnector);
                if (endPlaced)
                {
                    // 使用済みコネクターを記録（既存側とCorridor接続側）
                    usedConnectorPositions.Add(RoundVector3(existingConnector.position));
                    usedConnectorPositions.Add(RoundVector3(attachConnector.position));

                    // コネクターリスト更新：既存側を削除、Corridorの未使用コネクターを追加（残っていれば）
                    availableConnectors.Remove(existingConnector);
                    foreach (var c in corridorConnectors)
                    {
                        if (c != attachConnector && c != nextConnector)
                        {
                            availableConnectors.Add(c);
                        }
                    }

                    // NavMeshAgentを有効化
                    foreach (var a in corridorAgents) a.enabled = true;

                    // NavMesh再構築
                    if (navMeshSurface != null) navMeshSurface.BuildNavMesh();

                    placed = true;
                }
                else
                {
                    // ロールバック
                    spawnedRooms.Remove(corridor);
                    spawnedRoomBounds.Remove(corridorBounds);
                }

                if (placed) break;
            }

            if (placed)
            {
                return true;
            }
            else
            {
                Destroy(corridor);
            }
        }

        return false;
    }

    private bool TryConnectNewItem(GameObject itemPrefab, Transform specificConnector = null)
    {
        if (itemPrefab == null) return false;

        // specificConnectorが指定されていればそのまま委譲
        if (specificConnector != null)
        {
            return TryConnectNewItemToSpecificConnector_AllNewConnectors(itemPrefab, specificConnector);
        }

        // 未指定ならavailableConnectorsからランダムに選んで委譲
        if (availableConnectors == null || availableConnectors.Count == 0) return false;

        int index = Random.Range(0, availableConnectors.Count);
        Transform existingConnector = availableConnectors[index];
        return TryConnectNewItemToSpecificConnector_AllNewConnectors(itemPrefab, existingConnector);
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

        yield return null;
        yield return null;
        yield return null;



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

    // 位置丸めの小ヘルパー
    private Vector3 RoundVector3(Vector3 v)
    {
        return new Vector3(
            Mathf.Round(v.x * 100f) / 100f,
            Mathf.Round(v.y * 100f) / 100f,
            Mathf.Round(v.z * 100f) / 100f
        );
    }

    // 最終手段: 衝突判定を大幅に緩和してEndRoomを強制配置
    private bool ForcePlace_Relaxed(GameObject itemPrefab, Transform existingConnector)
    {
        if (existingConnector == null || itemPrefab == null) return false;

        GameObject newItem = Instantiate(itemPrefab);
        var navAgents = newItem.GetComponentsInChildren<NavMeshAgent>(true);
        foreach (var a in navAgents) a.enabled = false;

        var newItemConnectors = GetAllConnectors(newItem);
        if (newItemConnectors.Length == 0)
        {
            Debug.LogError($"プレハブ '{itemPrefab.name}' に Connector がありません。");
            Destroy(newItem);
            return false;
        }

        // シャッフル
        for (int i = 0; i < newItemConnectors.Length; i++)
        {
            int r = Random.Range(i, newItemConnectors.Length);
            (newItemConnectors[i], newItemConnectors[r]) = (newItemConnectors[r], newItemConnectors[i]);
        }

        foreach (var newItemConnector in newItemConnectors)
        {
            AlignObject(newItem, newItemConnector, existingConnector);

            // 緩和された衝突チェック: Expand値を大きくして許容範囲を広げる
            Bounds itemBounds = CalculateBounds(newItem);
            itemBounds.Expand(-5.0f); // 通常-2.0f → -5.0fでさらに許容

            bool collision = false;
            foreach (var existingBounds in spawnedRoomBounds)
            {
                if (existingBounds.Intersects(itemBounds))
                {
                    collision = true;
                    break;
                }
            }

            if (collision)
            {
                continue; // まだ衝突する場合は次へ
            }

            // 配置成功
            spawnedRooms.Add(newItem);
            spawnedRoomBounds.Add(CalculateBounds(newItem));

            usedConnectorPositions.Add(RoundVector3(existingConnector.position));
            usedConnectorPositions.Add(RoundVector3(newItemConnector.position));

            availableConnectors.Remove(existingConnector);
            foreach (var c in newItemConnectors)
            {
                if (c != newItemConnector)
                {
                    availableConnectors.Add(c);
                }
            }

            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
            foreach (var a in navAgents) a.enabled = true;

            return true;
        }

        // それでもダメなら諦めて破棄
        Destroy(newItem);
        return false;
    }
}