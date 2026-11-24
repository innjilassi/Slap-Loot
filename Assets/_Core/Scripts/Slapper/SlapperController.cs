using UnityEngine;
using Unity.Cinemachine;
using SlapGame; // Required for Cinemachine 3

[RequireComponent(typeof(CharacterController))]
public class SlapperController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SlapperInput input;
    [SerializeField] private SlapperAnimationHandler animHandler;
    [SerializeField] private CinemachineCamera cinemachineCam; // Drag your CM Camera here
    public bool rSlap;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float turnSpeed = 15f; 
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;

    public CharacterController Controller { get; private set; }
    public SlapperInput Input => input;
    public SlapperAnimationHandler Anim => animHandler;
    public float WalkSpeed => walkSpeed;
    
    public StateMachine StateMachine { get; private set; }
    private float _verticalVelocity;

    

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        if (input == null) input = GetComponent<SlapperInput>();
        if (animHandler == null) animHandler = GetComponentInChildren<SlapperAnimationHandler>();

        StateMachine = new StateMachine();
        StateMachine.Register(new IdleState(this));
        StateMachine.Register(new RunState(this));
    }

    private void Start() => StateMachine.ChangeState<IdleState>();

    private void Update()
    {
        StateMachine.Tick(Time.deltaTime);
        ApplyGravity();
    }
    public void Slap(SlapSide slapSide)
    {
        if(slapSide == SlapSide.Left) animHandler.SetTrigger("LSlap");
        else animHandler.SetTrigger("RSlap");
    }
    private void ApplyGravity()
    {
        if (Controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = groundedStick;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        Controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
    }

    public void Move(Vector3 worldDir, float speed)
    {
        Controller.Move(worldDir * speed * Time.deltaTime);
    }

    public void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    // --- CAMERA-RELATIVE MOVEMENT LOGIC ---

    public Vector3 GetMoveDirection()
    {
        Vector2 moveInput = input.MoveInput;

        if (cinemachineCam == null) 
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        Vector3 camForward = cinemachineCam.transform.forward;
        Vector3 camRight = cinemachineCam.transform.right;
        
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 dir = (camForward * moveInput.y + camRight * moveInput.x);
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    public Vector3 GetCameraForward()
    {
        if (cinemachineCam == null) return Vector3.forward;
        Vector3 forward = cinemachineCam.transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public bool GetIsSlapping()
    {
        return input.IsSlapping;
    }
}