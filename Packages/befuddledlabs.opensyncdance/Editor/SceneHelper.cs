using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3A.Editor;

namespace BefuddledLabs.OpenSyncDance.Editor
{
    public static class SceneHelper
    {
        public static List<OpenSyncDance> GetOpenSyncDances()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            // Linq my beloved
            return roots.Select(root => root.GetComponentsInChildren<OpenSyncDance>(false))
                .Where(syncDance => syncDance.Length != 0)
                .SelectMany(syncDance => syncDance,
                    (syncDance, syncDanceComponent) => new { syncDance, syncDanceComponent })
                .Where(t => t.syncDanceComponent.gameObject.activeInHierarchy)
                .Select(t => t.syncDanceComponent).ToList();
        }
    }
}