using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI設定")]
    public Image fadeImage;              // 暗転用の黒い画像
    public TextMeshProUGUI gameOverText; // 「GAME OVER」テキスト
    public float fadeDuration = 1.5f;    // 暗転にかかる時間（秒）
    public float textAppearDelay = 1.0f; // テキスト表示までの遅延時間

    private bool hasTriggered = false;   // 重複実行防止フラグ

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

        // フェード画像を作成（存在しない場合）
        if (fadeImage == null)
        {
            CreateFadeCanvas();
        }

        // テキストが存在しない場合は作成
        if (gameOverText == null)
        {
            CreateGameOverText();
        }
    }

    private void CreateFadeCanvas()
    {
        // Canvas作成
        GameObject canvasObj = new GameObject("GameOverCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998; // SceneTransitionManagerより手前
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

    private void CreateGameOverText()
    {
        // Canvas作成（テキスト用）
        GameObject canvasObj = new GameObject("GameOverTextCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最前面
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.transform.SetParent(transform);

        // TextMeshProオブジェクト作成
        GameObject textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(canvasObj.transform, false);
        gameOverText = textObj.AddComponent<TextMeshProUGUI>();

        // テキスト設定
        gameOverText.text = "GAME OVER";
        gameOverText.fontSize = 100;
        gameOverText.alignment = TextAlignmentOptions.Center;
        gameOverText.color = new Color(1, 0, 0, 0); // 最初は透明

        // RectTransform設定
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    public void TriggerGameOver()
    {
        // 重複実行を防止
        if (hasTriggered)
            return;

        hasTriggered = true;
        Time.timeScale = 0f; // ゲームを一時停止

        Debug.Log("ゲームオーバーが発動しました");
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // --- フェーズ1: 暗転 ---
        yield return StartCoroutine(FadeToBlack());

        // --- フェーズ2: テキスト表示 ---
        yield return StartCoroutine(FadeInGameOverText());

        // ここでリスタートボタンなどを表示する処理を追加可能
        Debug.Log("ゲームオーバー画面を表示中");
    }

    private IEnumerator FadeToBlack()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Time.scaleに影響されないようにする
            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    private IEnumerator FadeInGameOverText()
    {
        // テキスト表示までの遅延
        yield return new WaitForSecondsRealtime(textAppearDelay);

        float elapsedTime = 0f;
        Color textColor = gameOverText.color;
        float textFadeDuration = 1.0f;

        while (elapsedTime < textFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            textColor.a = Mathf.Clamp01(elapsedTime / textFadeDuration);
            gameOverText.color = textColor;
            yield return null;
        }

        textColor.a = 1f;
        gameOverText.color = textColor;
    }
}
