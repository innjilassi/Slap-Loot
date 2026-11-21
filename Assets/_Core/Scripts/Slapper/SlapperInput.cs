using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SlapperInput : MonoBehaviour
{
    public event Action OnSlapPressed;
    // --- Movement State ---
    public Vector2 MoveInput { get; private set; }
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;
    
    // --- Look State ---
    public Vector2 LookInput { get; private set; }

    // --- Action State ---
    public bool IsSlapping { get; private set; }

    private SlapLootInputSystem _input;

    private void Awake()
    {
        _input = new SlapLootInputSystem();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        _input.Slapper.Enable();

        _input.Slapper.Move.performed += OnMove;
        _input.Slapper.Move.canceled += OnMove;
        
        _input.Slapper.Look.performed += OnLook;
        _input.Slapper.Look.canceled += OnLook;

        _input.Slapper.SlapHold.performed += ctx =>
        {
            IsSlapping = true;
            OnSlapPressed?.Invoke();
        };
        _input.Slapper.SlapHold.canceled += ctx => IsSlapping = false;
    }

    private void OnDisable()
    {
        _input.Slapper.Move.performed -= OnMove;
        _input.Slapper.Move.canceled -= OnMove;
        
        _input.Slapper.Look.performed -= OnLook;
        _input.Slapper.Look.canceled -= OnLook;
        
        _input.Slapper.Disable();
    }

    private void OnMove(InputAction.CallbackContext ctx) => MoveInput = ctx.ReadValue<Vector2>();
    private void OnLook(InputAction.CallbackContext ctx) => LookInput = ctx.ReadValue<Vector2>();
}