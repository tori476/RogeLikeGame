using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Input Systemを使用

public class PlayerHP : MonoBehaviour
{
    [Header("HP Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("UI References")]
    public GameObject heartPrefab; // ← 作成したハートプレファブを設定
    public Transform heartsContainer; // ← HeartsContainerオブジェクトを設定

    private Image[] heartImages;

    void Start()
    {
        currentHealth = maxHealth;
        CreateHeartUI();
        UpdateHealthUI();
    }

    void CreateHeartUI()
    {
        heartImages = new Image[maxHealth];

        for (int i = 0; i < maxHealth; i++)
        {
            GameObject heartObj = Instantiate(heartPrefab, heartsContainer);
            heartImages[i] = heartObj.GetComponent<Image>();
        }
    }

    void UpdateHealthUI()
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (i < currentHealth)
            {
                heartImages[i].color = Color.red; // フルハート（赤色）
            }
            else
            {
                heartImages[i].color = Color.gray; // 空のハート（灰色）
            }
        }
    }

    // Input Systemを使用したテスト用キー入力
    void Update()
    {
        // キーボード入力の取得方法を変更
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Heal(1);
            Debug.Log("Hキーが押されました - ヒール実行");
        }

        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            TakeDamage(1);
            Debug.Log("Jキーが押されました - ダメージ実行");
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();

        Debug.Log($"ダメージを受けました。現在のHP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Debug.Log("プレイヤーのHPが0になりました");
        }
    }

    public void Heal(int healAmount)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();

        Debug.Log($"回復しました。現在のHP: {currentHealth}/{maxHealth}");
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}