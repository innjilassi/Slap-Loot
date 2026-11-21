using UnityEngine;

public class SlapperAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    private int _speedHash;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);
    }

    public void SetSpeed(float speed)
    {
        animator.SetFloat(_speedHash, speed);
    }

    public void SetTrigger(string trigger)
    {
        animator.SetTrigger(trigger);
    }
}