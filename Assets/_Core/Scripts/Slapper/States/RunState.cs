using UnityEngine;

public class RunState : IState
{
    private readonly SlapperController _ctx;
    public RunState(SlapperController ctx) => _ctx = ctx;

    public void Enter() => _ctx.Anim.SetSpeed(1f);

    public void Tick(float dt)
    {

        if (!_ctx.Input.HasMoveInput)
        {
            _ctx.StateMachine.ChangeState<IdleState>();
            return;
        }

        // 1. Get the direction based on WASD + Camera angle
        Vector3 moveDir = _ctx.GetMoveDirection();

        // 2. Face the direction we are actually moving (A = turn left, D = turn right)
        _ctx.FaceDirection(moveDir);
        
        // 3. Move
        _ctx.Move(moveDir, _ctx.WalkSpeed);
    }
    public void Exit() { }
}