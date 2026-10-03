using Xunit;
using xHotspot.Core.Models;
using xHotspot.Core.State;

public class StateMachineTests
{
    [Fact]
    public void InitialState_IsServiceStarting()
    {
        var sm = new StateMachine();
        Assert.Equal(ServiceState.ServiceStarting, sm.CurrentState);
    }

    [Fact]
    public void TransitionTo_UpdatesStateAndRaisesEvent()
    {
        var sm = new StateMachine();
        ServiceState? reportedState = null;
        sm.StateChanged += (sender, state) => reportedState = state;

        sm.TransitionTo(ServiceState.WaitingForNetwork);

        Assert.Equal(ServiceState.WaitingForNetwork, sm.CurrentState);
        Assert.Equal(ServiceState.WaitingForNetwork, reportedState);
    }
}
