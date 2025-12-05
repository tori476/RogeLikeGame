using UnityEngine;

public class GameBGMPlayer : MonoBehaviour
{
    public static GameBGMPlayer Instance { get; private set; }

    [Header("BGM設定")]
    [Tooltip("ループ再生したい音声ファイルをここに設定してください")]
    public AudioClip bgmNormal;

    public AudioClip bgmBattle;

    [Header("SE設定")]
    [Tooltip("アイテム取得時の効果音")]
    public AudioClip itemGetSound;

    private AudioSource audioSource;
    private AudioSource seAudioSource;

    void Awake()
    {
        // シングルトンの設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // BGM用AudioSourceコンポーネントを追加
        audioSource = gameObject.AddComponent<AudioSource>();

        // ループ再生の設定
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // SE用AudioSourceコンポーネントを追加
        seAudioSource = gameObject.AddComponent<AudioSource>();
        seAudioSource.loop = false;
        seAudioSource.playOnAwake = false;

        // 音声ファイルが設定されていれば再生開始
        if (bgmNormal != null)
        {
            audioSource.clip = bgmNormal;
            audioSource.Play();
        }
    }

    // シーン切り替え時に停止したい場合は、このメソッドを他のスクリプトから呼び出してください
    public void ChangeBGMBattle()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.clip = bgmBattle;
            audioSource.Play();
        }
    }

    public void ChangeBGMNormal()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.clip = bgmNormal;
            audioSource.Play();
        }
    }

    // アイテム取得時の効果音を再生
    public void PlayItemGetSound()
    {
        if (seAudioSource != null && itemGetSound != null)
        {
            seAudioSource.PlayOneShot(itemGetSound);
        }
    }
}
