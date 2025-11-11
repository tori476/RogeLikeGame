using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // シーン遷移に必要
using TMPro;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI設定")]
    public Image fadeImage;              // 暗転用の黒い画像
    public TextMeshProUGUI gameOverText; // 「GAME OVER」テキスト (Inspectorで割り当て)
    public float fadeDuration = 1.5f;    // 暗転にかかる時間（秒）
    public float textAppearDelay = 1.0f; // テキスト表示までの遅延時間

    [Header("シーン遷移設定")]
    public float resultSceneDelay = 3.0f; // ゲームオーバー画面表示後、Resultシーンへ遷移するまでの時間（秒）

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
    }

    private void CreateFadeCanvas()
    {
        // Canvas作成
        GameObject canvasObj = new GameObject("GameOverFadeCanvas");
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

        Debug.Log("ゲームオーバー画面を表示中");

        // --- フェーズ3: Resultシーンへ遷移 ---
        yield return new WaitForSecondsRealtime(resultSceneDelay);
        
        // 時間の流れを戻す
        Time.timeScale = 1f;
        
        // Resultシーンへ遷移
        SceneManager.LoadScene("Result");
    }

    private IEnumerator FadeToBlack()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    private IEnumerator FadeInGameOverText()
    {
        // テキストが設定されていない場合は処理をスキップ
        if (gameOverText == null)
        {
            Debug.LogWarning("GameOverTextがInspectorで設定されていません");
            yield break;
        }

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
