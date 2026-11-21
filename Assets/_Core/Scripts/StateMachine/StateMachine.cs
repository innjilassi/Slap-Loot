// StateMachine.cs
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }
    private Dictionary<System.Type, IState> _states = new();
    private System.Type _currentType;

    public void Register(IState state)
    {
        _states[state.GetType()] = state;
    }

    public void ChangeState<T>() where T : IState
    {
        var type = typeof(T);
        if (_currentType == type) return;
        if (!_states.ContainsKey(type))
        {
            Debug.LogError($"State {type.Name} not registered");
            return;
        }

        CurrentState?.Exit();
        CurrentState = _states[type];
        _currentType = type;
        CurrentState.Enter();
    }

    public void Tick(float deltaTime) => CurrentState?.Tick(deltaTime);
}