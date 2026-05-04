using UnityEngine;

public class TriggerOtherAnimation : MonoBehaviour
{
    public Animator targetAnimator;
    public string triggerName = "Activate";
    public string triggerName2 = "Deactivate";
    private bool activated = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;

        if (other.CompareTag("Player"))
        {
            targetAnimator.SetTrigger(triggerName);
            activated = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {

        if (collision.CompareTag("Player"))
        {
            targetAnimator.SetTrigger(triggerName2);
            activated = false;
        }
    }
}