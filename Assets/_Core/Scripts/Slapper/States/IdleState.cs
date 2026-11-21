using UnityEngine;

public class IdleState : IState
{
    private readonly SlapperController _ctx;
    public IdleState(SlapperController ctx) => _ctx = ctx;

    public void Enter() => _ctx.Anim.SetSpeed(0f);

    public void Tick(float dt)
    {
        // Face the camera while standing still
        _ctx.FaceDirection(_ctx.GetCameraForward());

        if (_ctx.Input.HasMoveInput)
        {
            _ctx.StateMachine.ChangeState<RunState>();
        }
    }
    public void Exit() { }
}