using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class ResetTitleButtonController : MonoBehaviour
{

    public Image fade;
    public void OnStartButtonClicked()
    {
        fade.DOFade(1f, 2f).OnComplete(() =>
        {
            SceneManager.LoadScene("TitleScene");
        });

    }
}
