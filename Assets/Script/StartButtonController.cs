using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Microsoft.Unity.VisualStudio.Editor;

public class StartButtonController : MonoBehaviour
{
    public Camera cam;

    public Image fade;
    public void OnStartButtonClicked()
    {
        cam.transform.DOMove(new Vector3(0, 0, 5f), 2f).SetEase(Ease.InOutBack);
        SceneManager.LoadScene("RoguLike");
    }
}
