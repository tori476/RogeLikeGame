using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButtonController : MonoBehaviour
{
    public void OnStartButtonClicked()
    {
        SceneManager.LoadScene("RoguLike");
    }
}
