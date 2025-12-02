using UnityEngine;
using UnityEngine.UI;

public class BossSelectorUI : MonoBehaviour
{
    [Header("UI要素")]
    public Image bossIconImage; // アイコンを表示するImageコンポーネント

    [Header("ボスアイコン画像")]
    public Sprite flyScorpionSprite;
    public Sprite redDragonSprite;
    public Sprite orcDragonSprite;
    public Sprite emptySprite; // 未選択またはアビリティなし用の画像

    // 現在の選択インデックスに基づいてUIを更新
    public void UpdateBossUI(int bossIndex, bool hasAbility)
    {
        // アビリティを持っていない場合は色を暗くするなどの演出も可能です
        bossIconImage.color = hasAbility ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);

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