using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("暗転設定")]
    public Image fadeImage; // 暗転用の黒い画像
    public float fadeDuration = 1.0f; // 暗転にかかる時間（秒）

    private void Awake()
    {
        // シングルトンパターン
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 暗転用の画像を作成（Canvasがない場合）
        if (fadeImage == null)
        {
            CreateFadeCanvas();
        }
    }

    private void CreateFadeCanvas()
    {
        // Canvas作成
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最前面に表示
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.transform.SetParent(transform);

        // 黒い画像を作成
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // 最初は透明
        fadeImage.raycastTarget = false;

        // 画面全体を覆うように設定
        RectTransform rectTransform = fadeImage.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    // 次のフロアに進むための暗転処理
    public void TransitionToNextFloor()
    {
        StartCoroutine(TransitionCoroutine());
    }

    private IEnumerator TransitionCoroutine()
    {
        Debug.Log("=== TransitionCoroutine 開始 ===");

        // プレイヤーの移動を無効化
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerController playerController = null;
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.SetMovementEnabled(false);
                Debug.Log("プレイヤーの移動を無効化しました");
            }
        }

        // フェードアウト（暗転）
        Debug.Log("フェードアウト開始");
        yield return StartCoroutine(FadeOut());
        Debug.Log("フェードアウト完了");

        // ★★★ 修正: 階層を先に増やす ★★★
        FloorUIController floorUI = FindFirstObjectByType<FloorUIController>();
        if (floorUI != null)
        {
            floorUI.IncreaseFloor();
            Debug.Log($"<color=yellow>【階層更新】階層を1つ上げました。現在: {floorUI.GetCurrentFloor()}F</color>");
        }
        else
        {
            Debug.LogWarning("FloorUIControllerが見つかりません！階層表示を更新できませんでした。");
        }

        // ★★★ その後でダンジョン再生成 ★★★
        DungeonManager dungeonManager = FindFirstObjectByType<DungeonManager>();
        if (dungeonManager != null)
        {
            Debug.Log("DungeonManagerを発見、ダンジョン再生成開始");
            dungeonManager.RegenerateDungeon();
            Debug.Log("ダンジョン再生成完了");
        }
        else
        {
            Debug.LogError("DungeonManagerが見つかりません！");
        }

        // プレイヤーをスタート地点に移動
        if (player != null)
        {
            player.transform.position = new Vector3(0, 10, 0);
        }

        // 少し待機
        Debug.Log("待機中...");
        yield return new WaitForSeconds(0.5f);

        // フェードイン（明転）
        Debug.Log("フェードイン開始");
        yield return StartCoroutine(FadeIn());
        Debug.Log("フェードイン完了");

        // プレイヤーの移動を再度有効化
        if (playerController != null)
        {
            playerController.SetMovementEnabled(true);
            Debug.Log("プレイヤーの移動を有効化しました");
        }

        Debug.Log("=== TransitionCoroutine 完了 ===");
    }

    // 暗転（フェードアウト）
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    // 明転（フェードイン）
    private IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            fadeImage.color = color;
            yield return null;
        }

        color.a = 0f;
        fadeImage.color = color;
    }
}
