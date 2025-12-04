using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private int damage;
    private GameObject owner;          // 発射した敵
    private Rigidbody rb;

    [Header("自動消去設定")]
    public float lifetime = 5.0f;      // 生存時間（秒）
    public float destroyDelay = 0.1f;  // ヒット後の消去遅延

    [Header("デバフ設定")]
    public float slowDebuffDuration = 10f;      // スロー効果の持続時間
    public float slowDebuffMultiplier = 0.5f;   // スピード減少倍率（50%に）

    private bool hasHit = false;       // ヒット済みフラグ
    private bool isInitialized = false; // 初期化済みフラグ

    // 効果音用の変数
    private AudioClip hitSound;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Rigidbodyの設定
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // スムーズな移動のため

        // Colliderの設定
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f; // 適切なサイズに調整

        // AudioSourceを取得または追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // AudioSourceの設定を最適化（2D音響に変更）
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 完全な2D音響（0=2D, 1=完全3D）
        audioSource.volume = 1.0f; // 音量を最大に
    }

    /// <summary>
    /// 球を初期化するメソッド
    /// </summary>
    public void Initialize(Vector3 dir, float spd, int dmg, GameObject own)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        owner = own;
        isInitialized = true;

        // 方向に応じて回転
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // 速度を設定
        rb.linearVelocity = direction * speed;

        // 一定時間後に自動削除
        Destroy(gameObject, lifetime);

        Debug.Log($"球を発射: 速度={speed}, 方向={direction}, ダメージ={damage}");
    }

    /// <summary>
    /// 命中音を設定するメソッド
    /// </summary>
    public void SetHitSound(AudioClip sound)
    {
        hitSound = sound;
    }

    private void FixedUpdate()
    {
        // 初期化されていない、ヒット済み、またはKinematicの場合は何もしない
        if (!isInitialized || hasHit || rb.isKinematic) return;

        // 速度を維持（念のため）
        if (rb.linearVelocity.magnitude < speed * 0.9f)
        {
            rb.linearVelocity = direction * speed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 既にヒット済みなら処理しない
        if (hasHit) return;

        // 発射した敵自身には当たらない
        if (other.gameObject == owner) return;

        // 部屋のColliderは無視する（Tag確認）
        if (other.CompareTag("Untagged") && other.gameObject.name.Contains("Room"))
        {
            Debug.Log($"部屋のColliderを無視: {other.gameObject.name}");
            return;
        }

        Debug.Log($"球が衝突: {other.gameObject.name}, Tag: {other.tag}");

        // Playerに当たった場合
        if (other.CompareTag("Player"))
        {
            // 命中音を再生
            PlayHitSound();

            PlayerHP playerHP = other.GetComponent<PlayerHP>();
            if (playerHP != null)
            {
                // ダメージは与えず、スピード減少デバフのみを適用
                playerHP.ApplySlowDebuff(slowDebuffDuration, slowDebuffMultiplier);
                Debug.Log($"Playerに{slowDebuffDuration}秒間のスピード減少デバフを適用！");
            }

            hasHit = true;
            DestroyProjectile();
        }
        // 壁に当たった場合
        else if (other.CompareTag("Wall"))
        {
            Debug.Log("球が壁に当たりました");
            hasHit = true;
            DestroyProjectile();
        }
        // 敵に当たった場合（他の敵には当たらないようにする）
        else if (other.CompareTag("Enemy"))
        {
            Debug.Log("球が敵に当たりましたが無視します");
            return;
        }
    }

    /// <summary>
    /// 命中音を再生
    /// </summary>
    private void PlayHitSound()
    {
        if (hitSound != null)
        {
            // 音を再生する独立したGameObjectを作成
            GameObject soundObject = new GameObject("HitSound");
            soundObject.transform.position = transform.position;

            AudioSource tempAudioSource = soundObject.AddComponent<AudioSource>();
            tempAudioSource.clip = hitSound;
            tempAudioSource.spatialBlend = 0f; // 2D音響
            tempAudioSource.volume = 1.0f;
            tempAudioSource.Play();

            // 音の長さ分待ってから削除
            Destroy(soundObject, hitSound.length);

            Debug.Log("命中音を独立したオブジェクトで再生しました");
        }
        else
        {
            Debug.LogWarning("hitSoundが設定されていません");
        }
    }

    private void DestroyProjectile()
    {
        // 移動を停止
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // コライダーを無効化（重複ヒット防止）
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // エフェクトを再生する場合はここに追加
        // Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

        // 遅延後に削除
        Destroy(gameObject, destroyDelay);
    }
}
