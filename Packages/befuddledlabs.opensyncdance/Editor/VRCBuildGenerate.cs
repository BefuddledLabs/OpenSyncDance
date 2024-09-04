#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BefuddledLabs.OpenSyncDance
{
    public class VRCBuildGenerate : MonoBehaviour, IEditorOnly
    {
        [InitializeOnLoadMethod]
        public static void RegisterSDKCallback()
        {
            VRCSdkControlPanel.OnSdkPanelEnable += AddBuildHook;
        }

        private static IVRCSdkAvatarBuilderApi _builder;

        private static void AddBuildHook(object sender, EventArgs e)
        {
            VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out _builder);
            _builder.OnSdkBuildStart += OnBuildStart;
        }

        private static void OnBuildStart(object sender, object e)
        {
            var syncDance = ((GameObject)e).GetComponentInChildren<OpenSyncDance>();
            if (!syncDance) return;

            syncDance.GenerateAll();
        }
    }
}

#endif