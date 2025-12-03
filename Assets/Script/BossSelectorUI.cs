using UnityEngine;
using UnityEngine.UI;

public class BossSelectorUI : MonoBehaviour
{
    [Header("UI要素")]
    public Image bossIconImage;

    [Header("ボスアイコン画像")]
    public Sprite flyScorpionSprite;
    public Sprite redDragonSprite;
    public Sprite orcDragonSprite;
    public Sprite emptySprite;

    // 現在の選択インデックスに基づいてUIを更新
    public void UpdateBossUI(int bossIndex, bool hasAbility)
    {
        // 何も持っていない場合(-1)は emptySprite を表示して終了
        if (bossIndex == -1 || !hasAbility)
        {
            bossIconImage.sprite = emptySprite;
            bossIconImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 半透明にするなど
            return;
        }

        // 所持している場合の表示
        bossIconImage.color = Color.white;

        switch (bossIndex)
        {
            case 0:
                bossIconImage.sprite = flyScorpionSprite;
                break;
            case 1:
                bossIconImage.sprite = redDragonSprite;
                break;
            case 2:
                bossIconImage.sprite = orcDragonSprite;
                break;
            default:
                bossIconImage.sprite = emptySprite;
                break;
        }
    }
}