using UnityEngine;
using System.Collections;

public class OrbitingProjectile : MonoBehaviour
{
    [Header("基本設定")]
    public float rotateSpeed = 100.0f;   // 回転速度
    public float radius = 2.0f;          // プレイヤーからの距離
    public float heightOffset = 1.0f;    // 地面からの高さ

    private float hitStopDuration = 0.05f; // ヒットストップの時間

    [Header("戦闘設定")]
    public int damage = 20;              // ダメージ量
    public float respawnDelay = 3.0f;    // 消滅してから復活するまでの時間

    private Transform playerTransform;   // プレイヤーの位置参照
    private float currentAngle;          // 現在の角度
    private Renderer meshRenderer;       // 見た目の制御用
    private Collider projectileCollider; // 当たり判定の制御用
    private bool isActive = true;        // 現在弾が有効かどうか

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        projectileCollider = GetComponent<Collider>();
    }

    // プレイヤーコントローラーから呼び出して初期化する
    public void Initialize(Transform targetPlayer)
    {
        playerTransform = targetPlayer;
    }

    void Update()
    {
        if (playerTransform == null) return;

        // プレイヤーの周囲を回転させる計算
        // 常に時間を加算して角度を更新
        currentAngle += rotateSpeed * Time.deltaTime;

        // 角度を0～360度に保つ（オーバーフロー防止）
        currentAngle %= 360f;

        // 三角関数を使って位置を決定 (円運動)
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, heightOffset, Mathf.Sin(rad) * radius);

        // プレイヤーの位置 + 計算したオフセット
        transform.position = playerTransform.position + offset;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 弾がアクティブでないなら処理しない
        if (!isActive) return;

        // "Enemy"タグを持つオブジェクトに当たった場合
        if (other.CompareTag("Enemy"))
        {
            // 1. ダメージを与える処理
            // EnemyAIスクリプトを取得してダメージを与える
            EnemyAI enemy = other.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, playerTransform);
                if (HitStop.Instance != null) //ヒットストップ呼び出し
                {
                    HitStop.Instance.Stop(hitStopDuration);
                }
            }

            // 汎用的なSendMessageを使う場合の例:
            other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"敵 {other.name} に {damage} ダメージを与えました！");

            // 2. 弾を一時的に消滅させ、復活コルーチンを開始
            StartCoroutine(RespawnRoutine());
        }
    }

    // ヒット後の復活処理コルーチン
    private IEnumerator RespawnRoutine()
    {
        isActive = false;

        // 見た目と当たり判定を無効化（オブジェクト自体はDestroyしない）
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (projectileCollider != null) projectileCollider.enabled = false;

        // 指定時間待機
        yield return new WaitForSeconds(respawnDelay);

        // 復活
        if (meshRenderer != null) meshRenderer.enabled = true;
        if (projectileCollider != null) projectileCollider.enabled = true;

        isActive = true;
    }
}