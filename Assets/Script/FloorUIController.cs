using UnityEngine;
using TMPro;

public class FloorUIController : MonoBehaviour
{
    [Header("UI要素への参照")]
    public TextMeshProUGUI floorText; // 階層を表示するテキスト

    private int currentFloor = 1; // 現在の階層(1から開始)

    void Start()
    {
        // 初期表示
        UpdateFloorDisplay();
    }

    /// <summary>
    /// 階層を1つ増やす
    /// </summary>
    public void IncreaseFloor()
    {
        currentFloor++;
        UpdateFloorDisplay();
        Debug.Log($"階層が上がりました: {currentFloor}階");
    }

    /// <summary>
    /// 階層を1にリセット
    /// </summary>
    public void ResetFloor()
    {
        currentFloor = 1;
        UpdateFloorDisplay();
        Debug.Log("階層を1階にリセットしました");
    }

    /// <summary>
    /// 現在の階層を取得
    /// </summary>
    public int GetCurrentFloor()
    {
        return currentFloor;
    }

    /// <summary>
    /// UIテキストを更新
    /// </summary>
    private void UpdateFloorDisplay()
    {
        if (floorText != null)
        {
            floorText.text = $"{currentFloor}F";
        }
        else
        {
            Debug.LogWarning("FloorTextが設定されていません!");
        }
    }
}
