#if UNITY_EDITOR
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
        public const string BootstrapID = "EdgegapServerBootstrap";
        public static string ProjectRootPath => Directory.GetCurrentDirectory();
        public static string ImportedBootstrapFolderPath =
            $"Assets{Path.DirectorySeparatorChar}{BootstrapID}";
        public static string AbsoluteBootstrapFolderPath =
            $"{ProjectRootPath}{Path.DirectorySeparatorChar}{ImportedBootstrapFolderPath}";
        public static string PluginRuntimeFolderPath =>
            $"{Directory.GetParent(Directory.GetFiles(ProjectRootPath, "Edgegap.asmdef", SearchOption.AllDirectories)[0]).FullName}{Path.DirectorySeparatorChar}Runtime";

        //internal static List<string> BootstrapModels =>
        //    new List<string>
        //    {
        //        "ArbitriumDeploymentLocation",
        //        "ArbitriumPortsMapping",
        //        "PortMappingData",
        //    };

        [MenuItem("GameObject/Edgegap Server Hosting/Port Verification - Unity NGO", priority = 35)]
        public static void ImportBootstrapUnityNGO() => ImportBootstrapToProject("UnityNGO");

        [MenuItem(
            "GameObject/Edgegap Server Hosting/Port Verification - Photon Fusion 2",
            priority = 36
        )]
        public static void ImportBootstrapPhotonFusion2() =>
            ImportBootstrapToProject("PhotonFusion2");

        [MenuItem("GameObject/Edgegap Server Hosting/Port Verification - Mirror", priority = 37)]
        public static void ImportBootstrapMirror() => ImportBootstrapToProject("Mirror");

        [MenuItem("GameObject/Edgegap Server Hosting/Port Verification - FishNet", priority = 38)]
        public static void ImportBootstrapFishNet() => ImportBootstrapToProject("FishNet");

        public static void ImportBootstrapToProject(string netcode)
        {
            if (string.IsNullOrEmpty(netcode))
            {
                Log("No netcode selected, host and port validation skipped.", true);
                return;
            }

            GameObject bootstrapInScene = GameObject.Find(BootstrapID);

            if (bootstrapInScene is not null)
            {
                Component[] bootstrapComponents = bootstrapInScene.GetComponents<Component>();
                string bootstrapType =
                    bootstrapComponents.Length >= 2
                        ? bootstrapComponents[1]?.GetType().ToString()
                        : null;

                if (bootstrapType is not null && !bootstrapType.Contains(netcode))
                {
                    // switched to different netcode
                    AssetDatabase.DeleteAsset(
                        $"{ImportedBootstrapFolderPath}{Path.DirectorySeparatorChar}{bootstrapType.Split(".")[2]}.cs"
                    );
                }

                if (bootstrapType is null || !bootstrapType.Contains(netcode))
                {
                    // deleted script asset but left behind gameobject in scene
                    UnityEngine.Object.DestroyImmediate(bootstrapInScene);
                }
                else
                {
                    Log($"{netcode} verification already present in current scene.");
                }
            }

            if (!Directory.Exists(AbsoluteBootstrapFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", BootstrapID);
            }

            // Initialize base bootstrap script
            if (
                !File.Exists(
                    $"{AbsoluteBootstrapFolderPath}{Path.DirectorySeparatorChar}{BootstrapID}.cs"
                )
            )
            {
                CloneTemplateScript("", $"{BootstrapID}Temp.cs.txt");
            }

            // @todo maybe not needed? => remove
            // Initialize each required API models
            //foreach (string model in BootstrapModels)
            //{
            //    if (
            //        !File.Exists(
            //            $"{AbsoluteBootstrapFolderPath}{Path.DirectorySeparatorChar}{model}.cs"
            //        )
            //    )
            //    {
            //        CloneTemplateScript(
            //            $"Models{Path.DirectorySeparatorChar}",
            //            $"{model}Temp.cs.txt"
            //        );
            //    }
            //}

            // Initialize netcode-specific bootstrap script
            string netcodeScriptName = $"{BootstrapID}{netcode}";

            if (
                !File.Exists(
                    $"{AbsoluteBootstrapFolderPath}{Path.DirectorySeparatorChar}{netcodeScriptName}.cs"
                )
            )
            {
                CloneTemplateScript(
                    $"BootstrapTemplates{Path.DirectorySeparatorChar}",
                    $"{netcodeScriptName}Temp.cs.txt"
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
                    .Where(t => t.Name == netcodeScriptName)
                    .First();

                GameObject bootstrapObj = new GameObject(BootstrapID);
                bootstrapObj.AddComponent(scriptType);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }

        private static void CloneTemplateScript(string sourcePath, string templateScriptName)
        {
            string fullTempScriptPath =
                $"{PluginRuntimeFolderPath}{Path.DirectorySeparatorChar}{sourcePath}{templateScriptName}";

            if (!File.Exists(fullTempScriptPath))
            {
                throw new Exception(
                    "Edgegap: Verification script template file not found; Contact the Edgegap support team for a bugfix."
                );
            }

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(
                ImportedBootstrapFolderPath,
                typeof(UnityEngine.Object)
            );
            AssetDatabase.OpenAsset(obj);

            string targetScriptName = templateScriptName.Replace("Temp", "").Replace(".txt", "");

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                fullTempScriptPath,
                targetScriptName
            );
        }

        private static void Log(string msg, bool warning = false)
        {
            string fullMessage = $"Edgegap: {msg}";
            if (warning)
            {
                Debug.LogWarning(fullMessage);
            }
            else
            {
                Debug.Log($"Edgegap: {msg}");
            }
        }
    }
}
#endif
