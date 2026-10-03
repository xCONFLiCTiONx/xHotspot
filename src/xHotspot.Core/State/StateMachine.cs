using xHotspot.Core.Models;

namespace xHotspot.Core.State;

public class StateMachine
{
    private ServiceState _currentState = ServiceState.ServiceStarting;
    public ServiceState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(this, _currentState);
            }
        }
    }

    public event EventHandler<ServiceState>? StateChanged;

    public void TransitionTo(ServiceState newState)
    {
        CurrentState = newState;
    }
}
