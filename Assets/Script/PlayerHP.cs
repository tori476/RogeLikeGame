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

    [Header("点滅設定")]
    public float blinkInterval = 0.1f; // 点滅の間隔（秒）
    private Coroutine blinkCoroutine;  // 点滅管理用コルーチン
    private Renderer[] playerRenderers; // プレイヤーのRendererコンポーネント配列

    [Header("UI References")]
    public GameObject heartPrefab;
    public Transform heartsContainer;

    private Image[] heartImages;

    [Header("ハートUIのRectTransform")]
    public RectTransform heartUIRectTransform; // Inspectorで割り当て

    [Header("ダメージ効果音設定")]
    public AudioClip damageSound; // 胸ぐらをつかむ音（通常のダメージを受けた時の効果音）
    public AudioClip enemyAIDamageSound; // EnemyAIからの攻撃を受けた時の効果音
    [Range(0f, 1f)]
    public float damageSoundVolume = 0.8f; // ダメージ音の音量
    private AudioSource audioSource;

    [Header("デバフ設定")]
    private bool isSlowed = false;              // スロー状態かどうか
    private float slowDuration = 10f;           // スロー効果の持続時間
    private float slowMultiplier = 0.5f;        // スピード減少倍率（50%に）
    private Coroutine slowCoroutine;            // スローコルーチンの参照

    private bool revivalItem = false;                   // 蘇生用アイテムを持っているか

    void Start()
    {
        currentHealth = maxHealth;
        CreateHeartUI();
        UpdateHealthUI();

        // プレイヤーのすべてのRendererコンポーネントを取得
        playerRenderers = GetComponentsInChildren<Renderer>();

        // AudioSourceコンポーネントを取得、なければ追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void CreateHeartUI()
    {
        if (heartsContainer != null)
        {
            foreach (Transform child in heartsContainer)
            {
                Destroy(child.gameObject);
            }
        }
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
            TakeDamage(1, false); // テスト用は音を鳴らさない
            Debug.Log("Jキーが押されました - ダメージ実行");
        }
    }

    // 既存のTakeDamageメソッド（後方互換性のため）
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false);
    }

    // EnemyAIからの攻撃かどうかを識別できるオーバーロード
    public void TakeDamage(int damage, bool isFromEnemyAI)
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

        // ダメージを受けたときに音を再生
        if (audioSource != null)
        {
            // EnemyAIからの攻撃の場合は2つの音を再生
            if (isFromEnemyAI)
            {
                // 胸ぐらをつかむ音（通常のダメージ音）
                if (damageSound != null)
                {
                    audioSource.PlayOneShot(damageSound, damageSoundVolume);
                }
                // EnemyAI専用の追加効果音
                if (enemyAIDamageSound != null)
                {
                    audioSource.PlayOneShot(enemyAIDamageSound, damageSoundVolume);
                }
            }
            else
            {
                // 通常のダメージの場合は胸ぐらをつかむ音のみ
                if (damageSound != null)
                {
                    audioSource.PlayOneShot(damageSound, damageSoundVolume);
                }
            }
        }

        // ダメージを受けたら必ず無敵時間を開始（死亡時も含む）
        StartInvincibility();

        // ダメージを受けたときにハートUIを揺らす
        if (heartUIRectTransform != null)
        {
            StartCoroutine(ShakeHeartUI());
        }

        // HP チェック
        if (currentHealth <= 0)
        {
            if (revivalItem == true)
            {
                currentHealth += 1;
                CreateHeartUI();
                UpdateHealthUI();
                revivalItem = false;
            }
            else
            {
                Debug.Log("プレイヤーのHPが0になりました");
                // ゲームオーバー処理
                GameOverManager.Instance.TriggerGameOver();
            }

        }
    }

    /// <summary>
    /// プレイヤーにスピード減少デバフを適用
    /// </summary>
    public void ApplySlowDebuff(float duration, float multiplier)
    {
        // 既にスローコルーチンが動いている場合は停止
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
        }

        slowDuration = duration;
        slowMultiplier = multiplier;
        slowCoroutine = StartCoroutine(SlowDebuffCoroutine());
    }

    private IEnumerator SlowDebuffCoroutine()
    {
        if (isSlowed)
        {
            // 既にスロー中の場合は一旦元に戻す
            RestoreNormalSpeed();
        }

        isSlowed = true;

        // PlayerControllerを取得してスピードを減少
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ApplySpeedMultiplier(slowMultiplier);
            Debug.Log($"スピード減少デバフ適用: {slowDuration}秒間、速度が{(1 - slowMultiplier) * 100}%減少");
        }

        // 持続時間待機
        yield return new WaitForSeconds(slowDuration);

        // 元の速度に戻す
        RestoreNormalSpeed();
    }

    private void RestoreNormalSpeed()
    {
        if (!isSlowed) return;

        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ApplySpeedMultiplier(1f);
            Debug.Log("スピード減少デバフが解除されました");
        }

        isSlowed = false;
    }

    public void MaxHP()
    {
        maxHealth += 1;
        currentHealth += 1;
        CreateHeartUI();
        UpdateHealthUI();
    }

    public void Revival()
    {
        revivalItem = true;
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

        // 点滅中なら停止
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }

        isInvincible = true; // 確実に無敵状態にする
        invincibilityCoroutine = StartCoroutine(InvincibilityCoroutine());
        blinkCoroutine = StartCoroutine(BlinkCoroutine());
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

        // 点滅を停止して、プレイヤーを確実に表示状態にする
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        SetPlayerVisibility(true);

        Debug.Log("無敵時間終了");
    }

    // 点滅の処理を行うコルーチン
    private IEnumerator BlinkCoroutine()
    {
        while (isInvincible)
        {
            // プレイヤーを非表示にする
            SetPlayerVisibility(false);
            yield return new WaitForSeconds(blinkInterval);

            // プレイヤーを表示する
            SetPlayerVisibility(true);
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    // プレイヤーの表示/非表示を切り替えるメソッド
    private void SetPlayerVisibility(bool isVisible)
    {
        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = isVisible;
            }
        }
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
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
        }
    }

    private IEnumerator ShakeHeartUI()
    {
        Vector3 originalPos = heartUIRectTransform.anchoredPosition;
        float shakeAmount = 20f;
        float shakeDuration = 0.2f;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float x = Mathf.Sin(elapsed * 40f) * shakeAmount;
            float y = Mathf.Cos(elapsed * 40f) * shakeAmount * 0.5f;
            heartUIRectTransform.anchoredPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        heartUIRectTransform.anchoredPosition = originalPos;
    }
}
