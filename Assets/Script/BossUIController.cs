using UnityEngine;
using UnityEngine.UI; // Sliderを使うために必要
using TMPro;          // TextMeshProを使うために必要

public class BossUIController : MonoBehaviour
{
    [Header("UI要素への参照")]
    public GameObject bossUIPanel;      // ボスUI全体のパネル
    public TextMeshProUGUI bossNameText; // ボスの名前を表示するテキスト
    public Slider healthSlider;         // ボスのHPを表示するスライダー

    private BossAI targetBoss; // 追跡対象のボス

    void Awake()
    {
        // ゲーム開始時はUIを非表示にしておく
        if (bossUIPanel != null)
        {
            bossUIPanel.SetActive(false);
        }
    }

    // ボスが出現した時にBossAIから呼び出されるメソッド
    public void SetupBossUI(BossAI boss)
    {
        targetBoss = boss;

        // ボスの名前とHPの最大値を設定
        bossNameText.text = targetBoss.gameObject.name;
        healthSlider.maxValue = targetBoss.health;
        healthSlider.value = targetBoss.health;

        // UIを表示する
        bossUIPanel.SetActive(true);

        // ボスの体力変動イベントと死亡イベントを購読する
        targetBoss.OnHealthChanged += UpdateHealthBar;
        targetBoss.OnBossDied += HideBossUI;
    }

    // HPが変動したときに呼び出されるメソッド
    private void UpdateHealthBar(int currentHealth)
    {
        healthSlider.value = currentHealth;
    }

    // ボスが倒された時に呼び出されるメソッド
    private void HideBossUI(BossAI boss)
    {
        bossUIPanel.SetActive(false);
    }

    // このオブジェクトが破棄される時にイベントの購読を解除する
    void OnDestroy()
    {
        if (targetBoss != null)
        {
            targetBoss.OnHealthChanged -= UpdateHealthBar;
            targetBoss.OnBossDied -= HideBossUI;
        }
    }
}