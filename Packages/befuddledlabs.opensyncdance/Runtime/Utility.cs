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
        static public void CreateBinarySearchTree(IAacFlMachineLayerWrapper parent, IAacFlDecisionParameter param, ref List<AacFlState> states, int depth = 0) {
            if (depth + 1 >= param.bitCount) {
                var state_0 = parent.NewState("0");
                var state_1 = parent.NewState("1");

                parent.EntryTransitionsTo(state_0).When(param.IsZeroBranch(depth));
                parent.EntryTransitionsTo(state_1);

                states.Add(state_0);
                states.Add(state_1);
            }
            else
            {
                var state_0 = parent.NewSubStateMachine($"SearchTree_{depth}_false");
                var state_1 = parent.NewSubStateMachine($"SearchTree_{depth}_true");

                state_0.Exits();
                state_1.Exits();

                parent.EntryTransitionsTo(state_0).When(param.IsZeroBranch(depth));
                parent.EntryTransitionsTo(state_1);

                CreateBinarySearchTree(new AacFlStateMachineWrapped(state_0), param.As0Branch(depth), ref states, depth + 1);
                CreateBinarySearchTree(new AacFlStateMachineWrapped(state_1), param.As1Branch(depth), ref states, depth + 1);
            }
        }
    }
}

#endif