using UnityEngine;

public class StairsTrigger : MonoBehaviour
{
    private bool triggered = false;
    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.CompareTag("Player"))
        {
            triggered = true;
            // フェードアウト処理がある場合はここで呼び出し、完了後にダンジョン再生成
            var fadeManagerObj = GameObject.FindObjectOfType(typeof(MonoBehaviour));
            if (fadeManagerObj != null && fadeManagerObj.GetType().Name == "FadeManager")
            {
                var fadeManager = fadeManagerObj as MonoBehaviour;
                var method = fadeManager.GetType().GetMethod("FadeOut");
                if (method != null)
                {
                    method.Invoke(fadeManager, new object[] { (System.Action)(() => {
                        DungeonManager manager = FindFirstObjectByType<DungeonManager>();
                        if (manager != null)
                        {
                            manager.RegenerateDungeon();
                            Debug.Log("暗転後にマップ地形を再生成しました");
                        }
                        else
                        {
                            Debug.LogWarning("DungeonManagerが見つかりませんでした");
                        }
                    }) });
                    return;
                }
            }
            // フェードが無い場合も必ずマップ再生成
            DungeonManager manager2 = FindFirstObjectByType<DungeonManager>();
            if (manager2 != null)
            {
                manager2.RegenerateDungeon();
                Debug.Log("暗転後にマップ地形を再生成しました");
            }
            else
            {
                Debug.LogWarning("DungeonManagerが見つかりませんでした");
            }
        }
    }
}
