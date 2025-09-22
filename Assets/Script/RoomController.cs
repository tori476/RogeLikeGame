using UnityEngine;

public class RoomController : MonoBehaviour
{
    // Inspectorから部屋の中にあるライトを登録するための配列
    public GameObject[] roomLights;

    // このメソッドは、Trigger内に他のColliderが入ってきた時に自動で呼び出される
    private void OnTriggerEnter(Collider other)
    {
        // 入ってきたオブジェクトのタグが "Player" だったら
        if (other.CompareTag("Player"))
        {
            // ライトを全てつける
            SetLights(true);
        }
    }

    // このメソッドは、Trigger内から他のColliderが出ていった時に自動で呼び出される
    private void OnTriggerExit(Collider other)
    {
        // 出ていったオブジェクトのタグが "Player" だったら
        if (other.CompareTag("Player"))
        {
            // ライトを全て消す
            SetLights(false);
        }
    }

    // ライトのON/OFFを切り替えるための補助メソッド
    private void SetLights(bool state)
    {
        foreach (var lightObject in roomLights)
        {
            if (lightObject != null)
            {
                lightObject.SetActive(state);
            }
        }
    }
}