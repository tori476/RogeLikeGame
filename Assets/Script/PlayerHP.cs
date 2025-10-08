using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Input Systemを使用
using System.Collections;

public class PlayerHP : MonoBehaviour
{
    [Header("HP Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("無敵時間設定")]
    public float invincibilityDuration = 1.5f; // 無敵時間（秒）
    private bool isInvincible = false;          // 無敵状態フラグ
    private Coroutine invincibilityCoroutine;   // 無敵時間管理用コルーチン

    [Header("UI References")]
    public GameObject heartPrefab;
    public Transform heartsContainer;

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
        // 無敵時間中はダメージを受けない
        if (isInvincible)
        {
            Debug.Log("無敵時間中のためダメージを無効化しました");
            return;
        }

        // ダメージ処理
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();

        Debug.Log($"ダメージを受けました。現在のHP: {currentHealth}/{maxHealth}");

        // ダメージを受けたら必ず無敵時間を開始（死亡時も含む）
        StartInvincibility();

        // HP チェック
        if (currentHealth <= 0)
        {
            Debug.Log("プレイヤーのHPが0になりました");
            // ゲームオーバー処理をここに追加可能
        }
    }

    public void Heal(int healAmount)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();

        Debug.Log($"回復しました。現在のHP: {currentHealth}/{maxHealth}");
    }

    // 無敵時間を開始するメソッド
    private void StartInvincibility()
    {
        // 既に無敵時間中なら、前のコルーチンを停止して新しく開始
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
            Debug.Log("前の無敵時間を停止して新しい無敵時間を開始");
        }

        isInvincible = true; // 確実に無敵状態にする
        invincibilityCoroutine = StartCoroutine(InvincibilityCoroutine());
    }

    // 無敵時間の処理を行うコルーチン
    private IEnumerator InvincibilityCoroutine()
    {
        Debug.Log("無敵時間開始");

        // 指定された時間だけ無敵状態を維持
        yield return new WaitForSeconds(invincibilityDuration);

        // 無敵時間終了
        isInvincible = false;
        invincibilityCoroutine = null;

        Debug.Log("無敵時間終了");
    }

    // 無敵状態かどうかを外部から確認できるメソッド
    public bool IsInvincible()
    {
        return isInvincible;
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    // オブジェクトが破棄される際にコルーチンを停止
    private void OnDestroy()
    {
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
        }
    }
}
