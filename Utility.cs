#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;

namespace OpenSyncDance
{
    static class Utils
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
        static public void CreateBinarySearchTree(IAacFlMachineLayerWrapper parent, AacFlBoolParameterGroup boolParams, int bitCount, ref List<AacFlState> states, int depth = 1) {
            var boolParamsList = boolParams.ToList();
            if (depth >= bitCount) {
                var zero = parent.NewState("0");
                var one = parent.NewState("1");

                parent.EntryTransitionsTo(zero).When(boolParamsList[depth-1].IsFalse());
                parent.EntryTransitionsTo(one);

                zero.Exits().When(boolParams.AreFalse());
                one.Exits().When(boolParams.AreFalse());

                states.Add(zero);
                states.Add(one);
            }
            else
            {
                var zero = parent.NewSubStateMachine($"SearchTree_{depth}_false");
                var one = parent.NewSubStateMachine($"SearchTree_{depth}_true");

                zero.Exits();
                one.Exits();

                parent.EntryTransitionsTo(zero).When(boolParamsList[depth-1].IsFalse());
                parent.EntryTransitionsTo(one);

                CreateBinarySearchTree(new AacFlStateMachineWrapped(zero), boolParams, bitCount, ref states, depth + 1);
                CreateBinarySearchTree(new AacFlStateMachineWrapped(one), boolParams, bitCount, ref states, depth + 1);
            }
        }
    }
}

#endif