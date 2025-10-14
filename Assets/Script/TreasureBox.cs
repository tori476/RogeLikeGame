using UnityEngine;
public class TreasureBox : MonoBehaviour
{
    private Animator anim;
    private bool hasPlayerEntered = false;

    private void Start()
    {
        anim = GetComponent<Animator>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasPlayerEntered)
        {
            hasPlayerEntered = true;
            anim.SetTrigger("open");
        }
    }
}
