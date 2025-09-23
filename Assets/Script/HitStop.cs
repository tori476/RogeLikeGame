using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance; // このスクリプトにどこからでもアクセスできるようにする
    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        // シングルトンパターンの実装
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ヒットストップを開始するメソッド
    public void Stop(float duration)
    {
        // 既にヒットストップ中なら、新しいもので上書きする
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(DoHitStop(duration));
    }

    private IEnumerator DoHitStop(float duration)
    {
        // 時間の流れを遅くする（0.1 = 10%の速度）
        Time.timeScale = 0.1f;
        // 物理演算の時間も合わせる
        Time.fixedDeltaTime = Time.timeScale * 0.02f;

        // 指定された時間（duration）だけ、現実の時間で待機する
        yield return new WaitForSecondsRealtime(duration);

        // 時間の流れを元に戻す
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = Time.timeScale * 0.02f;
        hitStopCoroutine = null;
    }
}