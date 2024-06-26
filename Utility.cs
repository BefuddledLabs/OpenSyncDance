#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

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

            return numberOfbits;
        }
    }
}

#endif