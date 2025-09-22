using UnityEngine;
using UnityEngine.InputSystem; // インプットシステムを使うために必要
using UnityEngine.Animations;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector2 moveInput; // 移動入力を保持する変数
    private Vector3 playerVelocity;

    private Animator anim;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
    }

    // Player Inputコンポーネントが "Move" アクションを検出したときに呼び出される
    // 関数名は "On" + アクション名 にするルール
    public void OnMove(InputAction.CallbackContext value)
    {
        // InputValueからVector2のデータを読み取り、変数に保存
        moveInput = value.ReadValue<Vector2>();
    }

    void Update()
    {
        // ----- 移動処理 -----
        // 2Dの入力(X,Y)を3Dの移動方向(X,Z)に変換
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);

        // 移動ベクトルがゼロより大きい（＝入力がある）場合のみ向きを変える
        if (moveDirection.magnitude > 0.1f)
        {
            // 指定したベクトル（moveDirection）の方向を向く回転を生成
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
        float Speed = new Vector2(moveDirection.x, moveDirection.z).magnitude;
        anim.SetFloat("speed", Speed);
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);


        // ----- 接地と重力処理 -----
        if (controller.isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = 0f;
        }

        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }
}