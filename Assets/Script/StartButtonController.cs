using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class StartButtonController : MonoBehaviour
{
    public Camera cam;

    public Image fade;
    public void OnStartButtonClicked()
    {
        cam.transform.DOMove(new Vector3(0, 0, 5f), 2f).SetEase(Ease.InOutBack);
        fade.DOFade(1f, 2f).OnComplete(() =>
        {
            SceneManager.LoadScene("RoguLike");
        });

    }
}
