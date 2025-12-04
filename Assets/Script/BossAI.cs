using UnityEngine;
using UnityEngine.AI;
using System;

// 抽象的なクラスにする（これ単体ではゲームオブジェクトにアタッチしない想定、または共通機能のみ提供）
public class BossAI : EnemyAI
{
    [Header("基本設定 (全ボス共通)")]
    public string bossName = "Boss"; // UI表示用の名前
    [Header("ドロップ・演出")]
    public GameObject bossSummonItemPrefab; // 討伐時のドロップ品
    public GameObject stairsPrefab;         // 次の階への階段

    // UI連携用のイベント
    public new event Action<int> OnHealthChanged;
    public event Action<BossAI> OnBossDied;

    protected BossUIController uiController; // 子クラスで使うかもしれないのでprotected

    protected override void Start()
    {
        base.Start(); // EnemyAIの初期化
        // 必要ならここで共通の初期化を行う
    }

    // Updateは子クラスで完全に上書き(Override)させるか、
    // ここでは何もしないようにしておく
    protected override void Update()
    {
        if (!isActivated || player == null) return;

        // ここには共通の処理があれば書くが、
        // 基本的に行動パターンは子クラス（Dragonなど）に任せる
        HandleBehavior();
    }

    // 子クラスが具体的な行動（AI）を記述するための仮想メソッド
    protected virtual void HandleBehavior()
    {
        // デフォルトでは何もしない
        // BossAI_Dragonなどでこれを override して行動を書く
    }

    // --- 共通機能: 起動 ---
    public override void ActivateEnemy()
    {
        if (isActivated) return;

        base.ActivateEnemy(); // isActivated = true になる

        Debug.Log($"{gameObject.name} (Boss) が起動しました！");

        GameBGMPlayer bgmPlayer = FindFirstObjectByType<GameBGMPlayer>();
        if (bgmPlayer != null)
        {
            bgmPlayer.ChangeBGMBattle();
        }

        // UIのセットアップ
        uiController = FindFirstObjectByType<BossUIController>();
        if (uiController != null)
        {
            uiController.SetupBossUI(this);
        }
    }

    // --- 共通機能: ダメージ管理 ---
    public override void TakeDamage(int damage, Transform attacker)
    {
        // EnemyAIの基本処理（HP減少など）
        // ただし base.TakeDamage を呼ぶと EnemyAI の死亡処理が走る可能性があるため、
        // HP計算だけ自分で行うか、EnemyAIの作りによっては base を呼ぶ。
        // 今回は EnemyAI の変数を直接操作します。

        health -= damage;
        OnHealthChanged?.Invoke(health); // UI更新通知

        Debug.Log($"{gameObject.name} の残り体力: {health}");

        // ノックバックが必要ならここで呼ぶ（ボスの場合はノックバックしないことも多いが）
        // if (knockbackCoroutine == null) StartCoroutine(Knockback(attacker));

        if (health <= 0)
        {
            Die();
        }
    }

    // --- 共通機能: 死亡処理 ---
    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} は倒された！");

        GameBGMPlayer bgmPlayer = FindFirstObjectByType<GameBGMPlayer>();
        if (bgmPlayer != null)
        {
            bgmPlayer.ChangeBGMNormal();
        }

        // ドロップアイテム
        if (bossSummonItemPrefab != null)
        {
            Instantiate(bossSummonItemPrefab, transform.position + Vector3.up, Quaternion.identity);
        }
        // 階段生成
        SpawnStairs();

        // UI非表示などの通知
        OnBossDied?.Invoke(this);

        Destroy(gameObject);
    }

    // 階段生成ロジック（長いのでメソッドに分離）
    private void SpawnStairs()
    {
        if (stairsPrefab == null) return;

        GameObject endRoom = GameObject.FindGameObjectWithTag("EndRoom");
        Vector3 stairsPos = transform.position;

        if (endRoom != null)
        {
            Renderer rend = endRoom.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                stairsPos = rend.bounds.center;
            }
            else
            {
                stairsPos = endRoom.transform.position;
            }
        }
        Instantiate(stairsPrefab, stairsPos, Quaternion.identity);
        Debug.Log("階段を出現させました");
    }
}