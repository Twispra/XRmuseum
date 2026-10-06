#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Museum.Experience.EditorTools
{
    /// <summary>Opens the saved production scene with its existing XR camera and controls.</summary>
    public static class HelmetExperiencePreview
    {
        [MenuItem("Museum/Experience/Open saved museum and play")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Museum/Scenes/MuseumStart.unity");
            EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
        }
    }
}
#endif
