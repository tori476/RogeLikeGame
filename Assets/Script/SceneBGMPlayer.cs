using UnityEngine;

public class SceneBGMPlayer : MonoBehaviour
{
    [Header("BGM設定")]
    [Tooltip("ループ再生したい音声ファイルをここに設定してください")]
    public AudioClip bgmClip;




    private AudioSource audioSource;

    void Start()
    {
        // AudioSourceコンポーネントを追加
        audioSource = gameObject.AddComponent<AudioSource>();

        // ループ再生の設定
        audioSource.loop = true;
        audioSource.playOnAwake = false;


        // 音声ファイルが設定されていれば再生開始
        if (bgmClip != null)
        {
            audioSource.clip = bgmClip;
            audioSource.Play();

        }

    }

    // シーン切り替え時に停止したい場合は、このメソッドを他のスクリプトから呼び出してください
    public void StopBGM()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

}
