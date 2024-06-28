#if UNITY_EDITOR

using AnimatorAsCode.V1;

namespace BefuddledLabs.OpenSyncDance
{
    /// <summary>
    /// Wraps around the common interface of state machines and layers to make generating search trees easier.
    /// </summary>
    public interface IAacFlMachineLayerWrapper
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

    /// <summary>
    /// Wraps around parameters that can be used to descend down a binary decision tree.
    /// </summary>
    public interface IAacFlDecisionParameter
    {
        int bitCount { get; set; }

        IAacFlCondition IsZeroBranch(int index);
        IAacFlCondition IsZeroed();
        IAacFlDecisionParameter As0Branch(int index);
        IAacFlDecisionParameter As1Branch(int index);
    }

    public struct AacFlBoolGroupDecisionParameter : IAacFlDecisionParameter
    {
        public AacFlBoolGroupDecisionParameter(AacFlBoolParameterGroup param, int numBits)
        {
            _wrapped = param;
            bitCount = numBits;
        }

        AacFlBoolParameterGroup _wrapped;

        public int bitCount { get; set; }

        public IAacFlCondition IsZeroBranch(int index)
            => _wrapped.ToList()[bitCount - index - 1].IsFalse();

        public IAacFlCondition IsZeroed()
            => _wrapped.AreFalse();

        public IAacFlDecisionParameter As0Branch(int index)
            => this;
        public IAacFlDecisionParameter As1Branch(int index)
            => this;
    }

    public struct AacFlIntDecisionParameter : IAacFlDecisionParameter
    {
        public AacFlIntDecisionParameter(AacFlIntParameter param, int numBits)
        {
            _wrapped = param;
            bitCount = numBits;
            _offset = 1 << (numBits - 1);
        }

        AacFlIntParameter _wrapped;

        public int bitCount { get; set; }

        int _offset;

        public IAacFlCondition IsZeroBranch(int index)
            => _wrapped.IsLessThan(_offset);

        public IAacFlCondition IsZeroed()
            => _wrapped.IsEqualTo(0);


        public IAacFlDecisionParameter As0Branch(int index)
        {
            var copy = this;
            copy._offset -= 1 << (bitCount - index - 2);
            return copy;
        }

        public IAacFlDecisionParameter As1Branch(int index)
        {
            var copy = this;
            copy._offset += 1 << (bitCount - index - 2);
            return copy;
        }
    }   
}

#endif