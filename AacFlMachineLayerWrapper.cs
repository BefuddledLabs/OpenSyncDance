#if UNITY_EDITOR

using AnimatorAsCode.V1;

namespace OpenSyncDance
{
    /// <summary>
    /// Wraps around the common interface of state machines and layers to make generating search trees easier.
    /// </summary>
    interface IAacFlMachineLayerWrapper
    {
        AacFlEntryTransition EntryTransitionsTo(AacFlState destination);
        AacFlEntryTransition EntryTransitionsTo(AacFlStateMachine destination);
        AacFlStateMachine NewSubStateMachine(string name);
        AacFlState NewState(string name);
        AacFlNewTransitionContinuation Exits();
    }

    public class AacFlStateMachineWrapped : IAacFlMachineLayerWrapper
    {
        public AacFlStateMachineWrapped(AacFlStateMachine machine)
        {
            _wrapped = machine;
        }

        private AacFlStateMachine _wrapped;

        public AacFlEntryTransition EntryTransitionsTo(AacFlState destination)
            => _wrapped.EntryTransitionsTo(destination);
        public AacFlEntryTransition EntryTransitionsTo(AacFlStateMachine destination)
            => _wrapped.EntryTransitionsTo(destination);
        public AacFlStateMachine NewSubStateMachine(string name)
            => _wrapped.NewSubStateMachine(name);
        public AacFlState NewState(string name)
            => _wrapped.NewState(name);
        public AacFlNewTransitionContinuation Exits() => _wrapped.Exits();

    }

    public class AacFlLayerWrapped : IAacFlMachineLayerWrapper
    {
        private AacFlLayer _wrapped;

        public AacFlEntryTransition EntryTransitionsTo(AacFlState destination)
            => _wrapped.EntryTransitionsTo(destination);
        public AacFlEntryTransition EntryTransitionsTo(AacFlStateMachine destination)
            => _wrapped.EntryTransitionsTo(destination);
        public AacFlStateMachine NewSubStateMachine(string name)
            => _wrapped.NewSubStateMachine(name);
        public AacFlState NewState(string name)
            => _wrapped.NewState(name);
        public AacFlNewTransitionContinuation Exits() { return null; }
    }
}

#endif