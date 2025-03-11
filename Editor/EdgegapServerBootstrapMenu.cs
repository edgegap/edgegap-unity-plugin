#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        public const string BootstrapObjectName = "EdgegapServerBootstrap";
        internal static string ProjectRootPath => Directory.GetCurrentDirectory();
        internal static string EdgegapBootstrapRuntimeFolderPath =>
            $"{Directory.GetParent(Directory.GetFiles(ProjectRootPath, "Edgegap.asmdef", SearchOption.AllDirectories)[0]).FullName}{Path.DirectorySeparatorChar}Runtime";
        internal static List<string> BootstrapModels =>
            new List<string>
            {
                "ArbitriumDeploymentLocation",
                "ArbitriumPortsMapping",
                "PortMappingData",
            };

        [MenuItem("Assets/Create/Edgegap/Server Bootstrap", priority = 35)]
        public static void InstantiateEdgegapBootstrap()
        {
            string netcode = EditorPrefs.GetString(EdgegapWindowMetadata.SELECTED_NETCODE_KEY_STR);

            if (string.IsNullOrEmpty(netcode))
            {
                Debug.LogWarning(
                    "No netcode currently selected in the Edgegap plugin, host and port validation skipped."
                );
                return;
            }
            else if (netcode == EdgegapWindowMetadata.Netcodes.Custom.ToString())
            {
                Debug.Log(
                    "Custom netcode selected; For information on netcodes not yet supported by the server bootstrap, contact the Edgegap support team."
                );
                return;
            }

            GameObject bootstrapInScene = GameObject.Find(BootstrapObjectName);

            if (bootstrapInScene is not null)
            {
                Component bootstrapComponent = bootstrapInScene.GetComponents<Component>()[1];
                string bootstrapType = bootstrapComponent.GetType().ToString();

                if (bootstrapType.Contains(netcode))
                {
                    Debug.Log(
                        $"Edgegap Server {netcode} Bootstrap is already present in the scene."
                    );
                    return;
                }

                // Delete gameObject + old bootstrap script
                string scriptName = bootstrapType.Split(".")[2];

                bool success = AssetDatabase.DeleteAsset(
                    $"Assets{Path.DirectorySeparatorChar}EdgegapServerBootstrap{Path.DirectorySeparatorChar}{scriptName}.cs"
                );

                UnityEngine.Object.DestroyImmediate(bootstrapInScene);
            }

            string assetBootstrapFolderPath =
                $"{ProjectRootPath}{Path.DirectorySeparatorChar}Assets{Path.DirectorySeparatorChar}EdgegapServerBootstrap";

            if (!Directory.Exists(assetBootstrapFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "EdgegapServerBootstrap");
            }

            // Initialize base bootstrap script
            string baseBootstrapInitScriptName = "EdgegapServerBootstrap";

            string baseBootstrapAssetFolderPath =
                $"Assets{Path.DirectorySeparatorChar}EdgegapServerBootstrap";

            string baseBootstrapFullInitPath =
                $"{ProjectRootPath}{Path.DirectorySeparatorChar}{baseBootstrapAssetFolderPath}{Path.DirectorySeparatorChar}{baseBootstrapInitScriptName}.cs";

            if (!File.Exists(baseBootstrapFullInitPath))
            {
                string baseTempScriptPath =
                    $"{EdgegapBootstrapRuntimeFolderPath}{Path.DirectorySeparatorChar}EdgegapServerBootstrapTemp.cs.txt";

                InitTemplateScript(
                    baseTempScriptPath,
                    baseBootstrapInitScriptName,
                    baseBootstrapAssetFolderPath
                );
            }

            // Initialize each required API models
            foreach (string model in BootstrapModels)
            {
                string modelBootstrapFullInitPath =
                    $"{ProjectRootPath}{Path.DirectorySeparatorChar}{baseBootstrapAssetFolderPath}{Path.DirectorySeparatorChar}{model}.cs";

                if (!File.Exists(modelBootstrapFullInitPath))
                {
                    string modelTempScriptPath =
                        $"{EdgegapBootstrapRuntimeFolderPath}{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}{model}Temp.cs.txt";

                    InitTemplateScript(modelTempScriptPath, model, baseBootstrapAssetFolderPath);
                }
            }

            // Initialize netcode-specific bootstrap script
            string netcodeBootstrapScriptName = $"EdgegapServerBootstrap{netcode}";

            string netcodeBootstrapAssetFolderPath =
                $"Assets{Path.DirectorySeparatorChar}EdgegapServerBootstrap";

            string netcodeBootstrapFullInitPath =
                $"{ProjectRootPath}{Path.DirectorySeparatorChar}{netcodeBootstrapAssetFolderPath}{Path.DirectorySeparatorChar}{netcodeBootstrapScriptName}.cs";

            if (!File.Exists(netcodeBootstrapFullInitPath))
            {
                string netcodeBootstrapTempScriptPath =
                    $"{EdgegapBootstrapRuntimeFolderPath}{Path.DirectorySeparatorChar}BootstrapTemplates{Path.DirectorySeparatorChar}{netcodeBootstrapScriptName}Temp.cs.txt";

                InitTemplateScript(
                    netcodeBootstrapTempScriptPath,
                    netcodeBootstrapScriptName,
                    netcodeBootstrapAssetFolderPath
                );

                Event recompileEvent = new Event();
                recompileEvent.keyCode = KeyCode.Return;
                recompileEvent.type = EventType.KeyDown;
                EditorWindow.focusedWindow.SendEvent(recompileEvent);
            }
            else
            {
                Type scriptType = TypeCache
                    .GetTypesDerivedFrom<Component>()
                    .Where(t => t.Name == netcodeBootstrapScriptName)
                    .First();

                GameObject bootstrapObj = new GameObject(BootstrapObjectName);
                bootstrapObj.AddComponent(scriptType);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }

        private static void InitTemplateScript(
            string tempScriptPath,
            string initScriptName,
            string initAssetPath
        )
        {
            if (!File.Exists(tempScriptPath))
            {
                throw new Exception(
                    "Template file not found; Contact the Edgegap support team about a plugin fix."
                );
            }

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(
                initAssetPath,
                typeof(UnityEngine.Object)
            );
            AssetDatabase.OpenAsset(obj);

#if UNITY_2019_1_OR_NEWER
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                tempScriptPath,
                $"{initScriptName}.cs"
            );
#else
            typeof(UnityEditor.ProjectWindowUtil)
                .GetMethod(
                    "CreateScriptAsset",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                )
                .Invoke(null, new object[] { tempScriptPath, $"{initScriptName}.cs" });
#endif
        }
    }
}
#endif
