using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace PancakeEditor
{
    internal static class MenuToolsCreator
    {
        #region 2

        [MenuItem("Tools/Project Settings", priority = 30000), UsedImplicitly]
        private static void OpenSettings()
        {
#if UNITY_2019_1_OR_NEWER
            SettingsService.OpenProjectSettings("Project/Player");
#else
			EditorApplication.ExecuteMenuItem("Edit/Project Settings...");
#endif
        }

        [MenuItem("Tools/User Preferences", priority = 30000), UsedImplicitly]
        private static void OpenPreferences()
        {
#if UNITY_2019_1_OR_NEWER
            SettingsService.OpenUserPreferences("Preferences/Pancake");
#else
			EditorApplication.ExecuteMenuItem("Edit/Project Settings...");
#endif
        }

        #endregion

        #region 3

        [MenuItem("Tools/Open Persistent Data Path", false, 31000), UsedImplicitly]
        private static void OpenPersistentDataPath() { EditorUtility.RevealInFinder(Application.persistentDataPath); }

        [MenuItem("Tools/Clear Persistent Data Path", false, 31000), UsedImplicitly]
        private static void ClearPersistentDataPath()
        {
            if (EditorUtility.DisplayDialog("Clear Persistent Data Path",
                    "Are you sure you wish to clear the persistent data path?\n This action cannot be reversed.",
                    "Clear",
                    "Cancel"))
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(Application.persistentDataPath);

                foreach (var file in di.GetFiles()) file.Delete();
                foreach (var dir in di.GetDirectories()) dir.Delete(true);
            }
        }

        [MenuItem("Tools/Clear PlayerPrefs", false, 31000), UsedImplicitly]
        private static void ClearPlayerPrefs()
        {
            if (EditorUtility.DisplayDialog("Clear PlayerPrefs", "Are you sure you wish to clear PlayerPrefs?\nThis action cannot be reversed.", "Clear", "Cancel"))
                PlayerPrefs.DeleteAll();
        }

        #endregion
    }
}