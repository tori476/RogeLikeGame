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
            // 暗転演出やマップ再生前にダンジョン再生成
            DungeonManager dungeonManager = FindObjectOfType<DungeonManager>();
            if (dungeonManager != null)
            {
                dungeonManager.RegenerateDungeon();
            }
            // 暗転や演出後にマップ地形再生成完了ログ
            Debug.Log("暗転後にマップ地形を再生成しました");
            // 必要ならここで演出やプレイヤー移動処理
        }
    }
}
