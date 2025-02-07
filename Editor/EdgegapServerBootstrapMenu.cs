#if UNITY_EDITOR
using Edgegap.Bootstrap;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Edgegap.Editor
{
    public static class EdgegapServerBootstrapMenu
    {
        internal static string ProjectRootPath => Directory.GetCurrentDirectory();
        internal static string EdgegapBootstrapTemplatesParentPath =>
            $"{Directory.GetParent(Directory.GetFiles(ProjectRootPath, "Edgegap.asmdef", SearchOption.AllDirectories)[0]).FullName}{Path.DirectorySeparatorChar}Runtime/BootstrapTemplates";

        [MenuItem("Assets/Create/Edgegap/Server Bootstrap", priority = 35)]
        public static void InstantiateEdgegapBootstrap()
        {
            if (GameObject.Find(EdgegapServerBootstrap.BootstrapObjectName) is not null)
            {
                Debug.Log("Edgegap Server Bootstrap is already present in the scene.");
                return;
            }

            string netcode = EditorPrefs.GetString(EdgegapWindowMetadata.SELECTED_NETCODE_KEY_STR);

            if (string.IsNullOrEmpty(netcode))
            {
                Debug.LogWarning(
                    "No netcode currently selected in the Edgegap plugin, host and port validation skipped."
                );
                return;
            }

            string templateScriptName = $"EdgegapServerBootstrap{netcode}Temp";
            string assetFolderScriptPath =
                $"{ProjectRootPath}{Path.DirectorySeparatorChar}Assets{Path.DirectorySeparatorChar}{templateScriptName}.cs";

            if (!File.Exists(assetFolderScriptPath))
            {
                string templateScriptPath =
                    $"{EdgegapBootstrapTemplatesParentPath}{Path.DirectorySeparatorChar}{templateScriptName}.cs.txt";

                if (!File.Exists(templateScriptPath))
                {
                    Debug.LogWarning($"Template script for {netcode} netcode could not be found.");
                    return;
                }

                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(
                    "Assets",
                    typeof(UnityEngine.Object)
                );
                AssetDatabase.OpenAsset(obj);

#if UNITY_2019_1_OR_NEWER
                ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                    templateScriptPath,
                    $"{templateScriptName}.cs"
                );
#else
                typeof(UnityEditor.ProjectWindowUtil)
                    .GetMethod(
                        "CreateScriptAsset",
                        System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(null, new object[] { templateScriptPath, $"{templateScriptName}.cs" });
#endif
                Event recompileEvent = new Event();
                recompileEvent.keyCode = KeyCode.Return;
                recompileEvent.type = EventType.KeyDown;
                EditorWindow.focusedWindow.SendEvent(recompileEvent);
            }
            else
            {
                Type scriptType = TypeCache
                    .GetTypesDerivedFrom<Component>()
                    .Where(t => t.Name == templateScriptName)
                    .First();

                GameObject bootstrapObj = new GameObject("EdgegapServerBootstrap");
                bootstrapObj.AddComponent(scriptType);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }
    }
}
#endif
