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

    // 武器のダメージ処理スクリプトへの参照
    private WeaponDamageDealer weaponDamageDealer;


    private bool isDashing = false;
    private bool isCharging = false;
    private float chargeStartTime;

    private CharacterController controller;
    private Vector2 moveInput; // 移動入力を保持する変数
    private Vector3 playerVelocity;

    private bool isMovementLocked = false;

    private Animator anim;

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

        controller.Move(moveDirection * currentSpeed * Time.deltaTime);
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
}