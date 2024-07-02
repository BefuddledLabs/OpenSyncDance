#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
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
    abstract public class AacFlDecisionParameter : ICloneable
    {
        public AacFlDecisionParameter(int numBits)
        {
            bitCount = numBits;
        }

        /// <summary>
        /// The number of bits in the binary tree.
        /// </summary>
        readonly public int bitCount;

        /// <summary>
        /// The index of the represented state.
        /// </summary>
        public int id = 0;

        /// <summary>
        /// The number of bits we have encoded.
        /// </summary>
        public int depth = 0;

        /// <summary>
        /// Condition to get exactly to this state.
        /// </summary>
        public abstract void EntryCondition(AacFlNewTransitionContinuation condition);

        /// <summary>
        /// Condition to be not in this state.
        /// </summary>
        public abstract void ExitCondition(AacFlNewTransitionContinuation condition);

        /// <summary>
        /// Condition to enter the 0 branch.
        /// </summary>
        public abstract IAacFlCondition Is0Branch();

        /// <summary>
        /// Enter the 0 branch.
        /// </summary>
        /// <returns></returns>
        public AacFlDecisionParameter As0Branch()
        {
            AacFlDecisionParameter copy = (AacFlDecisionParameter)Clone();
            copy.depth++;
            return copy;
        }

        /// <summary>
        /// Enter the 1 branch.
        /// </summary>
        public AacFlDecisionParameter As1Branch()
        {
            // basically a 0 branch with a bit set
            AacFlDecisionParameter copy = As0Branch();
            copy.id |= 1 << GetBitIndex();
            return copy;
        }

        /// <summary>
        /// Get the encoded bits.
        /// </summary>
        public IEnumerable<bool> GetBits()
            => Enumerable.Range(0, bitCount).Select(GetBit);

        /// <summary>
        /// Get the encoded bit at an index.
        /// </summary>
        public bool GetBit(int index)
            => (1 & (id >> index)) > 0;

        /// <summary>
        /// Get the index of the bit we are checking.
        /// </summary>
        public int GetBitIndex()
            => bitCount - depth - 1;

        /// <summary>
        /// Make a copy.
        /// </summary>
        public object Clone()
            => MemberwiseClone();
    }

    public class AacFlBoolGroupDecisionParameter : AacFlDecisionParameter
    {
        public AacFlBoolGroupDecisionParameter(AacFlBoolParameterGroup param, int numBits) : base(numBits)
        {
            _wrapped = param;
        }

        readonly AacFlBoolParameterGroup _wrapped;

        private IEnumerable<(AacFlBoolParameter param, bool bit)> GetBitComparisons()
            => _wrapped.ToList().Zip<AacFlBoolParameter, bool, (AacFlBoolParameter, bool)>(GetBits(), (p, b) => new (p, b));

        public override void EntryCondition(AacFlNewTransitionContinuation condition)
            => condition.When(_wrapped.AreFalseExcept(GetBitComparisons().Where(x => x.bit).Select(x => x.param).ToArray()));
        public override void ExitCondition(AacFlNewTransitionContinuation conditions) {
            var cs = GetBitComparisons().Select(x => x.param.IsNotEqualTo(x.bit));
            cs.Skip(1).Aggregate(conditions.WhenConditions().And(cs.First()), (c, other) => c.Or().When(other));
        }

        public override IAacFlCondition Is0Branch()
            => GetBitComparisons().Reverse().ElementAt(depth).param.IsFalse();
    }

    public class AacFlIntDecisionParameter : AacFlDecisionParameter
    {
        public AacFlIntDecisionParameter(AacFlIntParameter param, int numBits) : base(numBits)
        {
            _wrapped = param;
        }

        readonly AacFlIntParameter _wrapped;

        public override void EntryCondition(AacFlNewTransitionContinuation condition)
            => condition.When(_wrapped.IsEqualTo(id));

        public override void ExitCondition(AacFlNewTransitionContinuation condition)
            => condition.When(_wrapped.IsNotEqualTo(id));

        public override IAacFlCondition Is0Branch()
            => _wrapped.IsLessThan(As1Branch().id);
    }   
}

#endif