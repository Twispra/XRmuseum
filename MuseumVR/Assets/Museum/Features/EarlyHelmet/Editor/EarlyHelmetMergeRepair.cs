#if UNITY_EDITOR
using System;
using Museum.PhysicalHands.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Museum.EarlyHelmet.EditorTools
{
    /// <summary>Explicit maintenance of the installed assets; never reinstalls the model or scene.</summary>
    public static class EarlyHelmetMergeRepair
    {
        public static void RunBatch()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play before repairing saved font and grip cache.");
                TrilingualFontBaker.RunBatch();
                MuseumGripSurfaceBaker.BakeSavedHelmet();
                AssetDatabase.SaveAssets();
                Debug.Log("EARLY_MERGE_REPAIR_PASS font identities retained; imported OBJ grip surfaces saved; scene not saved.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
#endif
