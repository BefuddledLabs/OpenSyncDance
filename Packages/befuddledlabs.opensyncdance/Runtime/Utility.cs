#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;

namespace BefuddledLabs.OpenSyncDance
{
    public static class Utils
    {
        /// <summary>
        /// Get the minimum number of bits required to represent a value.
        /// </summary>
        /// <param name="value">The value that needs to be represented.</param>
        /// <returns>The number of bits.</returns>
        static public int NumberOfBitsToRepresent(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            int numberOfbits = 0;
            while ((value >>= 1) != 0)
                numberOfbits++;

            return numberOfbits + 1;
        }

        /// <summary>
        /// Creates a binary tree that maps a bool parameter list to a series of states.
        /// </summary>
        static public IEnumerable<(AacFlState state, AacFlDecisionParameter param)> CreateBinarySearchTree(IAacFlMachineLayerWrapper parent, AacFlDecisionParameter param) {
            // if next layer is last

            if (param.depth + 1 >= param.bitCount) {
                var state_0 = parent.NewState($"bit[{param.GetBitIndex()}] == 0");
                var state_1 = parent.NewState($"bit[{param.GetBitIndex()}] == 1");

                parent.EntryTransitionsTo(state_0).When(param.Is0Branch());
                parent.EntryTransitionsTo(state_1);

                yield return (state_0, param.As0Branch());
                yield return (state_1, param.As1Branch());
            }
            else
            {
                var state_0 = parent.NewSubStateMachine($"bit[{param.GetBitIndex()}] == 0");
                var state_1 = parent.NewSubStateMachine($"bit[{param.GetBitIndex()}] == 1");

                state_0.Exits();
                state_1.Exits();

                parent.EntryTransitionsTo(state_0).When(param.Is0Branch());
                parent.EntryTransitionsTo(state_1);

                // recurse
                foreach (var result in CreateBinarySearchTree(new AacFlStateMachineWrapped(state_0), param.As0Branch()))
                    yield return result;
                foreach (var result in CreateBinarySearchTree(new AacFlStateMachineWrapped(state_1), param.As1Branch()))
                    yield return result;
            }
        }
    }
}

#endif