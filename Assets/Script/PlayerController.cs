using UnityEngine;
using UnityEngine.InputSystem; // インプットシステムを使うために必要
using UnityEngine.Animations;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5.0f;
    public float dashSpeed = 10.0f;
    public float gravity = -9.81f;

    [Header("攻撃設定")]
    // この時間（秒）より短くボタンを押した場合、通常攻撃になる
    public float tapAttackThreshold = 0.3f;

    public float attackCooldown = 0.5f;

    public float normalAttackDuration = 0.6f; //アニメーション再生時間

    private bool canAttack = true;

    [Header("溜め攻撃設定")]
    public float minChargeForce = 8.0f;  // 最小の飛び出し力
    public float maxChargeForce = 20.0f; // 最大の飛び出し力
    public float maxChargeDuration = 2.0f; // 最大溜め時間

    public float chargeAttackDuration = 1.0f; //アニメーション再生時間

    [Header("武器設定")]
    [SerializeField]
    private Collider weaponCollider;

    [Header("ボス召喚設定")]

    public BossSelectorUI bossSelectorUI;
    public GameObject summonedBossFlyScorpionPrefab; // 召喚するボスのプレハブ

    public GameObject summonedBossRedDragonPrefab;

    public GameObject summonedBossOrcPrefab;
    public Transform summonPoint;         // ボスを召喚する位置

    //アビリティの所持フラグ
    public bool hasBossFlyScorpionSummonAbility = false;

    public bool hasBossRedDragonSummonAbility = false;

    public bool hasBossOrcSummonAbility = false;

    //ボス選択の変数
    private int currentBossIndex = 0; // 0:Scorpion, 1:RedDragon, 2:Orc
    private int maxBossCount = 3;

    private Transform lockOnTarget; // ロックオン対象
    private bool isLockingOn = false; // ロックオン状態

    // 武器のダメージ処理スクリプトへの参照
    private WeaponDamageDealer weaponDamageDealer;

    private bool isDashing = false;
    private bool isCharging = false;

    [Header("アイテム設定")]
    public GameObject orbitProjectilePrefab; // 回転する弾のプレハブ（OrbitingProjectileスクリプト付き）
    private GameObject currentOrbitProjectile; // 生成済みの弾を保持する変数

    [Header("ソードショット設定")]
    public GameObject swordShotPrefab; // 飛び道具のプレハブ (インスペクターで設定)
    public Transform swordShotSpawnPoint; // 発射位置 (インスペクターで設定。なければプレイヤー位置)

    private bool hasSwordShotAbility = false; // ソードショット能力を持っているか判定するBool

    private bool isChargingAttack = false;
    private float chargeStartTime;

    private CharacterController controller;
    private Vector2 moveInput; // 移動入力を保持する変数
    private Vector3 playerVelocity;

    private bool isMovementLocked = false;

    private Animator anim;

    private InputSystem_Actions inputActions;

    private float speedMultiplier = 1f;  // スピード倍率（デバフ用）

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        if (weaponCollider != null)
        {
            weaponDamageDealer = weaponCollider.GetComponent<WeaponDamageDealer>();
            if (weaponDamageDealer != null)
            {
                weaponDamageDealer.Initialize(this.transform);
            }
        }
        UpdateBossSelectionUI();
    }

    void OnEnable()
    {
        inputActions.Player.LockOn.performed += OnLockOn;
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Player.LockOn.performed -= OnLockOn;
        inputActions.Disable();
    }

    // Player Inputコンポーネントが "Move" アクションを検出したときに呼び出される
    // 関数名は "On" + アクション名 にするルール
    public void OnMove(InputAction.CallbackContext value)
    {
        // 溜め攻撃中は移動入力を受け付けない
        if (isCharging)
        {
            moveInput = Vector2.zero;
            return;
        }
        // InputValueからVector2のデータを読み取り、変数に保存
        moveInput = value.ReadValue<Vector2>();
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isDashing = true;
        }
        else if (context.canceled)
        {
            isDashing = false;
        }
    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (!canAttack)
        {
            return;
        }
        // ボタンが押された瞬間の処理
        if (context.started)
        {
            if (!isChargingAttack)
            {
                // 【通常攻撃】
                anim.SetTrigger("Attack");
                StartCoroutine(LockMovementForDuration(normalAttackDuration));
                return;
            }
            isCharging = true;
            chargeStartTime = Time.time;
        }

        // ボタンが離された瞬間の処理
        if (context.canceled && isCharging)
        {
            isCharging = false;
            anim.SetBool("IsCharging", false); // 溜めモーション終了

            float holdDuration = Time.time - chargeStartTime;

            //canAttack = false;
            //StartCoroutine(ResetAttackCooldown()); //クールダウン


            // ■ 短いタップか、長いホールドかを判定
            if (holdDuration < tapAttackThreshold)
            {
                // 【通常攻撃】
                anim.SetTrigger("Attack");
                StartCoroutine(LockMovementForDuration(normalAttackDuration));
            }
            else
            {
                // 【溜め攻撃】
                // 溜め時間を0秒から最大溜め時間の間で制限
                // tapAttackThresholdを引くことで、溜め始めの時間を調整
                float chargeDuration = Mathf.Clamp(holdDuration - tapAttackThreshold, 0, maxChargeDuration);

                // 溜め時間の割合（0.0～1.0）を計算
                float chargeRatio = chargeDuration / maxChargeDuration;

                int chargeDamage = (int)(25 * (1.0f + chargeRatio)); // 1は基本ダメージ。WeaponDamageDealerの基本値と合わせる
                if (weaponDamageDealer != null)
                {
                    weaponDamageDealer.SetDamage(chargeDamage);
                }

                // 割合に応じて、最小と最大の間で飛び出す力を決定
                float force = Mathf.Lerp(minChargeForce, maxChargeForce, chargeRatio);

                StartCoroutine(PerformChargeAttack(force));
                anim.SetTrigger("ChargeAttack");
                StartCoroutine(LockMovementForDuration(chargeAttackDuration));
            }
        }
    }

    public void OnSwitchBoss(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            float value = context.ReadValue<float>();

            // 入力値が一定以上なら切り替え（スティックの誤入力防止）
            if (Mathf.Abs(value) > 0.5f)
            {
                if (value > 0)
                {
                    currentBossIndex = (currentBossIndex + 1) % maxBossCount;
                }
                else
                {
                    currentBossIndex = (currentBossIndex - 1 + maxBossCount) % maxBossCount;
                }

                UpdateBossSelectionUI();
                Debug.Log("Current Boss Index: " + currentBossIndex);
            }
        }
    }

    // Input Actionsで "SummonBoss" を定義してください
    public void OnSummonBoss(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            TrySummonCurrentBoss();
        }
    }

    // 現在選択中のボスを召喚しようとする処理
    private void TrySummonCurrentBoss()
    {
        switch (currentBossIndex)
        {
            case 0: // Fly Scorpion
                if (hasBossFlyScorpionSummonAbility) SummonFlyScorpionBoss();
                else Debug.Log("Scorpionのアビリティを持っていません");
                break;
            case 1: // Red Dragon
                if (hasBossRedDragonSummonAbility) SummonRedDragonBoss();
                else Debug.Log("Red Dragonのアビリティを持っていません");
                break;
            case 2: // Orc Dragon
                if (hasBossOrcSummonAbility) SummonOrcBoss(); // ※SummonOrcBossメソッドの実装が必要です
                else Debug.Log("Orcのアビリティを持っていません");
                break;
        }
        UpdateBossSelectionUI(); // 消費後にUI（色など）を更新するため
    }

    // UI更新ヘルパー
    private void UpdateBossSelectionUI()
    {
        if (bossSelectorUI == null) return;

        bool hasCurrentAbility = false;
        switch (currentBossIndex)
        {
            case 0: hasCurrentAbility = hasBossFlyScorpionSummonAbility; break;
            case 1: hasCurrentAbility = hasBossRedDragonSummonAbility; break;
            case 2: hasCurrentAbility = hasBossOrcSummonAbility; break;
        }

        bossSelectorUI.UpdateBossUI(currentBossIndex, hasCurrentAbility);
    }

    // アビリティ付与メソッドの更新（UI更新を追加）
    public void GrantBossFlyScorpionSummonAbility()
    {
        hasBossFlyScorpionSummonAbility = true;
        Debug.Log("Scorpion Ability Get!");
        UpdateBossSelectionUI();
    }

    public void GrantBossRedDragonSummonAbility()
    {
        hasBossRedDragonSummonAbility = true;
        UpdateBossSelectionUI();
    }

    public void GrantBossOrcSummonAbility()
    {
        hasBossOrcSummonAbility = true;
        UpdateBossSelectionUI();
    }

    public void OnLockOn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (!isLockingOn)
            {
                // 最も近い敵をロックオン
                lockOnTarget = FindNearestEnemy();
                if (lockOnTarget != null)
                {
                    isLockingOn = true;
                }
            }
            else
            {
                // ロックオン解除
                isLockingOn = false;
                lockOnTarget = null;
            }
        }
    }

    public void StartAttack()
    {
        if (weaponCollider != null)
        {
            // 武器のColliderを有効化
            weaponCollider.enabled = true;

            // ヒット済みリストをリセットする
            if (weaponDamageDealer != null)
            {
                weaponDamageDealer.StartDealDamage();
            }
        }
    }

    public void EndAttack()
    {
        if (weaponCollider != null)
        {
            // 武器のColliderを無効化
            weaponCollider.enabled = false;
        }

        // 念のため、通常攻撃のダメージ量に戻しておく
        if (weaponDamageDealer != null)
        {
            weaponDamageDealer.SetDamage(25); // 25は基本ダメージ
        }
    }

    public void FireSwordShot()
    {
        // 能力を持っていない、またはプレハブが設定されていない場合は何もしない
        if (!hasSwordShotAbility || swordShotPrefab == null)
        {
            return;
        }

        // 発射位置の決定（SpawnPointがなければプレイヤーの前方少し上）
        Vector3 spawnPos = swordShotSpawnPoint != null ? swordShotSpawnPoint.position : transform.position + transform.forward + Vector3.up;

        // プレイヤーの向きに合わせて発射
        Instantiate(swordShotPrefab, spawnPos, transform.rotation * Quaternion.Euler(90f, 0f, 0f));
    }
    private IEnumerator ResetAttackCooldown()
    {
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    private IEnumerator LockMovementForDuration(float duration)
    {
        isMovementLocked = true;
        yield return new WaitForSeconds(duration);
        isMovementLocked = false;
    }



    //チャージ攻撃を可能にする
    public void ChargeAttackItem()
    {
        isChargingAttack = true;
    }

    public void SwordShotItem()
    {
        hasSwordShotAbility = true;
    }

    public void ComboAttackItem()
    {
        anim.SetBool("ComboAttack", true);
    }

    public void NoneItem()
    {
        if (orbitProjectilePrefab == null)
        {
            Debug.LogWarning("OrbitProjectilePrefab が設定されていません！インスペクターで設定してください。");
            return;
        }

        // 既に弾を生成済みの場合は、重複生成しない（あるいはリセットする等の仕様ならここを変える）
        if (currentOrbitProjectile != null)
        {
            Debug.Log("既に回転弾は有効です。");
            return;
        }

        // 弾を生成
        currentOrbitProjectile = Instantiate(orbitProjectilePrefab, transform.position, Quaternion.identity);

        // 生成した弾のスクリプトを取得し、プレイヤー情報を渡して初期化
        OrbitingProjectile orbScript = currentOrbitProjectile.GetComponent<OrbitingProjectile>();
        if (orbScript != null)
        {
            orbScript.Initialize(this.transform);
        }

        Debug.Log("回転弾アイテムを使用しました！");
    }

    // 召喚処理
    private void SummonFlyScorpionBoss()
    {
        if (summonedBossFlyScorpionPrefab == null) { Debug.LogError("Prefab Missing"); return; }
        Vector3 spawnPosition = summonPoint != null ? summonPoint.position : transform.position + transform.forward * 3.0f;
        GameObject boss = Instantiate(summonedBossFlyScorpionPrefab, spawnPosition, transform.rotation);
        // SummonedFlyScorpionBoss summonedBoss = boss.GetComponent<SummonedFlyScorpionBoss>(); 
        hasBossFlyScorpionSummonAbility = false; // 消費
        Debug.Log("Scorpion Summoned");
    }

    private void SummonRedDragonBoss()
    {
        if (summonedBossRedDragonPrefab == null) { Debug.LogError("Prefab Missing"); return; }
        Vector3 spawnPosition = summonPoint != null ? summonPoint.position : transform.position + transform.forward * 3.0f;
        GameObject boss = Instantiate(summonedBossRedDragonPrefab, spawnPosition, transform.rotation);
        hasBossRedDragonSummonAbility = false; // 消費
        Debug.Log("Red Dragon Summoned");
    }

    // Orc用の召喚メソッドを追加（仮実装）
    private void SummonOrcBoss()
    {
        if (summonedBossOrcPrefab == null) return;
        Vector3 spawnPosition = summonPoint != null ? summonPoint.position : transform.position + transform.forward * 3.0f;
        Instantiate(summonedBossOrcPrefab, spawnPosition, transform.rotation);
        hasBossOrcSummonAbility = false;
        Debug.Log("Orc Summoned");
    }



    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = Mathf.Infinity;
        foreach (var enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }
        return nearest;
    }

    public void ResetVelocity()
    {
        playerVelocity = Vector3.zero;
    }

    void Update()
    {
        if (isCharging)
        {
            // 【変更点】ボタンを押し続けて、通常攻撃の時間を超えたら溜めアニメーションを開始する
            if (Time.time - chargeStartTime > tapAttackThreshold)
            {
                // IsChargingがfalseの場合のみtrueに設定する（一度だけ実行するため）
                if (!anim.GetBool("IsCharging"))
                {
                    anim.SetBool("IsCharging", true);
                }
            }

            // 溜め中は移動処理を行わない
            return;
        }

        if (isLockingOn && lockOnTarget != null)
        {
            // プレイヤーの向きをロックオン対象に向ける
            Vector3 direction = (lockOnTarget.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
            }
        }

        HandleMovement();
        HandleGravity();
    }

    private void HandleMovement()
    {
        if (isMovementLocked)
        {
            // ロック中はアニメーターの速度も0にする
            anim.SetFloat("speed", 0);
            return;
        }
        float currentSpeed = isDashing ? dashSpeed : moveSpeed;
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);

        if (moveDirection.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        float animSpeed = moveDirection.magnitude;
        anim.SetFloat("speed", isDashing ? animSpeed * 2.0f : animSpeed);

        controller.Move(moveDirection * currentSpeed * speedMultiplier * Time.deltaTime);
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }
    private IEnumerator PerformChargeAttack(float force)
    {
        float duration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            controller.Move(transform.forward * force * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// スピード倍率を設定（デバフ用）
    /// </summary>
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }
}