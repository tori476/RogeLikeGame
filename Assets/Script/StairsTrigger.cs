using UnityEngine;
using System.Collections;

public class StairsTrigger : MonoBehaviour
{
    [Header("フェード設定")]
    public CanvasGroup fadeCanvasGroup; // フェード用のCanvasGroup
    public float fadeDuration = 0.5f;   // フェードの長さ

    private bool isTriggered = false;   // 重複実行防止フラグ

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーが階段に触れたかチェック
        if (other.CompareTag("Player") && !isTriggered)
        {
            isTriggered = true;
            Debug.Log("階段に触れました。次の階層へ移動します...");

            // SceneTransitionManagerを使って暗転処理を開始
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToNextFloor();
            }
            else
            {
                Debug.LogError("SceneTransitionManagerが見つかりません！");
            }
        }
    }

    private IEnumerator GoToNextFloor()
    {
        Debug.Log("GoToNextFloor開始");

        // フェードアウト（画面を暗くする）
        if (fadeCanvasGroup != null)
        {
            Debug.Log("フェードアウト開始");
            yield return StartCoroutine(Fade(0f, 1f));
            Debug.Log("フェードアウト完了");
        }
        else
        {
            Debug.LogWarning("CanvasGroupが設定されていません");
            yield return new WaitForSeconds(0.3f);
        }

        // ダンジョンマネージャーを取得してマップを再生成
        DungeonManager dungeonManager = FindObjectOfType<DungeonManager>();
        if (dungeonManager != null)
        {
            Debug.Log("DungeonManagerを発見。マップ再生成を開始します...");
            dungeonManager.RegenerateDungeon();
            Debug.Log("マップ再生成完了");
        }
        else
        {
            Debug.LogError("DungeonManagerが見つかりません！");
        }

        // 少し待機してからフェードイン
        Debug.Log("待機中...");
        yield return new WaitForSeconds(0.5f);

        // フェードイン（画面を明るくする）
        if (fadeCanvasGroup != null)
        {
            Debug.Log("フェードイン開始");
            yield return StartCoroutine(Fade(1f, 0f));
            Debug.Log("フェードイン完了");
        }

        // フラグをリセット
        isTriggered = false;
        Debug.Log("階層移動完了！");
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = endAlpha;
    }
}
