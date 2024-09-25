#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Edgegap.Editor.Api;
using Edgegap.Editor.Api.Models;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml, superceding` EdgegapWindow.cs`.
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        #region Vars
        public static bool IsLogLevelDebug =>
            EdgegapWindowMetadata.LOG_LEVEL == EdgegapWindowMetadata.LogLevel.Debug;
        private bool IsInitd;
        private VisualTreeAsset _visualTree;
        private bool _isApiTokenVerified; // Toggles the rest of the UI

        private ApiEnvironment _apiEnvironment; // TODO: Swap out hard-coding with UI element?
        private GetRegistryCredentialsResult _credentials;
        private static readonly Regex _appNameAllowedCharsRegex = new Regex(@"^[a-zA-Z0-9_\-+\.]*$"); // MIRROR CHANGE: 'new()' not supported in Unity 2020
        private GetCreateAppResult _loadedApp;
        private string _deploymentRequestId;
        private string _userExternalIp;

        private string _containerRegistryUrl;
        private string _containerProject;
        private string _containerUsername;
        private string _containerToken;

        private List<string> _existingAppNames = null;
        private List<string> _storedDeployAppVersions = null;
        #endregion // Vars

        #region Vars -> Interactable Elements
        private Button _debugBtn;
        private VisualElement _postAuthContainer;

        private VisualElement _signInContainer;
        private Button _edgegapSignInBtn;
        private VisualElement _connectedContainer;
        private Button _signOutBtn;
        private Button _joinEdgegapDiscordBtn;
        private Button _termsOfServicesLink;
        private TextField _apiTokenInput;
        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;

        private Foldout _serverBuildFoldout;
        private Button _infoLinuxRequirementsBtn;
        private Button _installLinuxRequirementsBtn;
        private Label _linuxRequirementsResultLabel;
        private Button _buildParamsBtn;
        private TextField _buildFolderNameInput;
        private Button _serverBuildBtn;
        private Label _serverBuildResultLabel;

        private Foldout _containerizeFoldout;
        private Button _infoDockerRequirementsBtn;
        private Button _validateDockerRequirementsBtn;
        private Label _dockerRequirementsResultLabel;
        private TextField _buildPathInput;
        private Button _buildPathResetBtn;
        private TextField _containerizeImageNameInput;
        private TextField _containerizeImageTagInput;
        private TextField _dockerfilePathInput;
        private Button _dockerfilePathResetBtn;
        private TextField _optionalDockerParamsInput;
        private Button _containerizeServerBtn;
        private Label _containerizeServerResultLabel;

        private Foldout _createAppFoldout;
        private TextField _createAppNameInput;
        private TextField _serverImageNameInput;
        private TextField _serverImageTagInput;
        private Button _portMappingLabelLink;
        private Button _uploadImageCreateAppBtn;
        private Button _appInfoLabelLink;
        private Button _createAppNameShowDropdownBtn;

        private Foldout _deployAppFoldout;
        private TextField _deployAppNameInput;
        private TextField _deployAppVersionInput;
        private Button _deployLimitLabelLink;
        private Button _deployAppBtn;
        private Button _stopLastDeployBtn;
        private Button _discordHelpBtn;
        private Label _deployResultLabel;
        private Button _deployAppNameShowDropdownBtn;
        private Button _deployAppVersionShowDropdownBtn;

        private Foldout _nextStepsFoldout;
        private Button _serverConnectLink;
        private Button _lobbyMatchmakerLabelLink;
        private Button _lifecycleManageLabelLink;
        #endregion //Vars -> Interactable Elements

        // MIRROR CHANGE
        // get the path of this .cs file so we don't need to hardcode paths to
        // the .uxml and .uss files:
        // https://forum.unity.com/threads/too-many-hard-coded-paths-in-the-templates-and-documentation.728138/
        // this way users can move this folder without breaking UIToolkit paths.
        internal string StylesheetPath =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
        // END MIRROR CHANGE
        internal string ProjectRootPath => Directory.GetCurrentDirectory();
        internal string DefaultDockerFilePath => $"{Directory.GetParent(Directory.GetFiles(ProjectRootPath, GetType().Name + ".cs", SearchOption.AllDirectories)[0]).FullName}{Path.DirectorySeparatorChar}Dockerfile";
        internal string DefaultFolderName => "EdgegapServer";

        [MenuItem("Tools/Edgegap Hosting")] // MIRROR CHANGE: more obvious title
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindowV2 window = GetWindow<EdgegapWindowV2>();
            window.titleContent = new GUIContent("Edgegap Hosting"); // MIRROR CHANGE: 'Edgegap Server Management' is too long for the tab space
            window.maxSize = new Vector2(635, 900);
            window.minSize = window.maxSize;
        }

        #region Unity Funcs
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
// check if defined first, otherwise adding the symbol causes an infinite loop of recompilation
#if !EDGEGAP_PLUGIN_SERVERS
            // Get data about current target group
            bool standaloneAndServer = false;
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            if (buildTargetGroup == BuildTargetGroup.Standalone)
            {
                StandaloneBuildSubtarget standaloneSubTarget = EditorUserBuildSettings.standaloneBuildSubtarget;
                if (standaloneSubTarget == StandaloneBuildSubtarget.Server)
                    standaloneAndServer = true;
            }

            // Prepare named target, depending on above stuff
            NamedBuildTarget namedBuildTarget;
            if (standaloneAndServer)
                namedBuildTarget = NamedBuildTarget.Server;
            else
                namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // Set universal compiler macro
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, $"{PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget)};{EdgegapWindowMetadata.KEY_COMPILER_MACRO}");
#endif
        }

        protected void OnEnable()
        {
#if UNITY_2021_3_OR_NEWER // only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // Set root VisualElement and style: V2 still uses EdgegapWindow.[uxml|uss]
            // BEGIN MIRROR CHANGE
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{StylesheetPath}{Path.DirectorySeparatorChar}EdgegapWindow.uxml");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesheetPath}{Path.DirectorySeparatorChar}EdgegapWindow.uss");
            // END MIRROR CHANGE
            rootVisualElement.styleSheets.Add(styleSheet);
#endif
        }

#pragma warning disable CS1998 // disable async warning in U2020
        public async void CreateGUI()
#pragma warning restore CS1998
        {
            // the UI requires 'GroupBox', which is not available in Unity 2019/2020.
            // showing it will break all of Unity's Editor UIs, not just this one.
            // instead, show a warning that the Edgegap plugin only works on Unity 2021+
#if !UNITY_2021_3_OR_NEWER
            Debug.LogWarning("The Edgegap Hosting plugin requires UIToolkit in Unity 2021.3 or newer. Please upgrade your Unity version to use this.");
#else
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();
            syncFormWithObjectStatic();
            await syncFormWithObjectDynamicAsync(); // API calls

            IsInitd = true;
#endif
        }

        /// <summary>The user closed the window. Save the data.</summary>
        protected void OnDisable()
        {
#if UNITY_2021_3_OR_NEWER // only load stylesheet in supported Unity versions, otherwise it shows errors in U2020
            // sometimes this is called without having been registered, throwing NRE
            if (_debugBtn == null) return;

            unregisterClickEvents();
            unregisterFieldCallbacks();
#endif
        }
        #endregion // Unity Funcs


        #region Init
        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        private void InitUIElements()
        {
            setVisualElementsToFields();
            closeDisableGroups();
            registerClickCallbacks();
            registerFieldCallbacks();
            initToggleDynamicUi();
        }

        private void closeDisableGroups()
        {
            _serverBuildFoldout.value = false;
            _containerizeFoldout.value = false;
            _createAppFoldout.value = false;
            _deployAppFoldout.value = false;
            _nextStepsFoldout.value = false;

            _serverBuildFoldout.SetEnabled(false);
            _containerizeFoldout.SetEnabled(false);
            _createAppFoldout.SetEnabled(false);
            _deployAppFoldout.SetEnabled(false);
            _nextStepsFoldout.SetEnabled(false);
        }

        /// <summary>Set fields referencing UI Builder's fields. In order of appearance from top-to-bottom.</summary>
        private void setVisualElementsToFields()
        {
            _debugBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEBUG_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID);

            _signInContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.SIGN_IN_CONTAINER_ID);
            _edgegapSignInBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SIGN_IN_BTN_ID);
            _connectedContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.CONNECTED_CONTAINER_ID);
            _signOutBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SIGN_OUT_BTN_ID);
            _joinEdgegapDiscordBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.JOIN_DISCORD_BTN_ID);
            _termsOfServicesLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.TERMS_OF_SERVICES_LINK_ID);
            _apiTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID);
            _apiTokenGetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID);

            _serverBuildFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.SERVER_BUILD_FOLDOUT_ID);
            _infoLinuxRequirementsBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.LINUX_REQUIREMENTS_LINK_ID);
            _installLinuxRequirementsBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.INSTALL_LINUX_BTN_ID);
            _linuxRequirementsResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.INSTALL_LINUX_RESULT_LABEL_ID);
            _buildParamsBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SERVER_BUILD_PARAM_BTN_ID);
            _buildFolderNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.SERVER_BUILD_FOLDER_TXT_ID);
            _serverBuildBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.SERVER_BUILD_BTN_ID);
            _serverBuildResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.SERVER_BUILD_RESULT_LABEL_ID);

            _containerizeFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.CONTAINERIZE_SERVER_FOLDOUT_ID);
            _infoDockerRequirementsBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DOCKER_INSTALL_LINK_ID);
            _validateDockerRequirementsBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.VALIDATE_DOCKER_INSTALL_BTN_ID);
            _dockerRequirementsResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.VALIDATE_DOCKER_RESULT_LABEL_ID);
            _buildPathInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINERIZE_SERVER_BUILD_PATH_TXT_ID);
            _buildPathResetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CONTAINERIZE_BUILD_PATH_RESET_BTN_ID);
            _containerizeImageNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINERIZE_IMAGE_NAME_TXT_ID);
            _containerizeImageTagInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINERIZE_IMAGE_TAG_TXT_ID);
            _dockerfilePathInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.DOCKERFILE_PATH_TXT_ID);
            _dockerfilePathResetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DOCKERFILE_PATH_RESET_BTN_ID);
            _optionalDockerParamsInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.DOCKER_BUILD_PARAMS_TXT_ID);
            _containerizeServerBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CONTAINERIZE_SERVER_BTN_ID);
            _containerizeServerResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.CONTAINERIZE_SERVER_RESULT_LABEL_TXT);

            _createAppFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.CREATE_APP_FOLDOUT_ID);
            _createAppNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CREATE_APP_NAME_TXT_ID);
            _createAppNameShowDropdownBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CREATE_APP_NAME_SHOW_DROPDOWN_BTN_ID);
            _serverImageNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CREATE_APP_IMAGE_NAME_TXT_ID);
            _serverImageTagInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CREATE_APP_IMAGE_TAG_TXT_ID);
            _portMappingLabelLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.PORT_MAPPING_LABEL_LINK_ID);
            _uploadImageCreateAppBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.PUSH_IMAGE_CREATE_APP_BTN_ID);
            _appInfoLabelLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.EDGEGAP_APP_LABEL_LINK_ID);

            _deployAppFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.DEPLOY_APP_FOLDOUT_ID);
            _deployAppNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.DEPLOY_APP_NAME_TXT_ID);
            _deployAppVersionInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.DEPLOY_APP_TAG_VERSION_TXT_ID);
            _deployLimitLabelLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_LIMIT_LABEL_LINK_ID);
            _deployAppBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_START_BTN_ID);
            _stopLastDeployBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_STOP_BTN_ID);
            _discordHelpBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_DISCORD_HELP_BTN_ID);
            _deployResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.DEPLOY_RESULT_LABEL_TXT);
            _deployAppNameShowDropdownBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_APP_NAME_SHOW_DROPDOWN_BTN_ID);
            _deployAppVersionShowDropdownBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOY_APP_VERSION_SHOW_DROPDOWN_BTN_ID);

            _nextStepsFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.NEXT_STEPS_FOLDOUT_ID);
            _serverConnectLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.NEXT_STEPS_SERVER_CONNECT_LINK_ID);
            _lobbyMatchmakerLabelLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.NEXT_STEPS_LOBBY_MATCHMAKER_LABEL_LINK_ID);
            _lifecycleManageLabelLink = rootVisualElement.Q<Button>(EdgegapWindowMetadata.NEXT_STEPS_LIFECYCLE_LABEL_LINK_ID);

            _apiEnvironment = EdgegapWindowMetadata.API_ENVIRONMENT; // (!) TODO: Hard-coded while unused in UI
        }

        /// <summary>
        /// Register non-btn change actionss. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerFieldCallbacks()
        {
            _apiTokenInput.RegisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.RegisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _buildPathInput.RegisterCallback<FocusEvent>(OnBuildPathInputFocus);

            _dockerfilePathInput.RegisterCallback<FocusEvent>(OnDockerfilePathInputFocus);
            _containerizeImageNameInput.RegisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageTagInput.RegisterValueChangedCallback(OnContainerizeInputsChanged);

            _createAppNameInput.RegisterValueChangedCallback(OnCreateAppNameInputChanged);
            _serverImageNameInput.RegisterValueChangedCallback(OnCreateInputsChanged);
            _serverImageTagInput.RegisterValueChangedCallback(OnCreateInputsChanged);

            _deployAppNameInput.RegisterValueChangedCallback(OnDeployAppNameInputChanged);
            _deployAppVersionInput.RegisterValueChangedCallback(OnDeployAppVersionInputChanged);
        }

        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// Should parity the opposite of registerFieldCallbacks().
        /// </summary>
        private void unregisterFieldCallbacks()
        {
            _apiTokenInput.UnregisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.UnregisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _buildPathInput.UnregisterCallback<FocusEvent>(OnBuildPathInputFocus);

            _dockerfilePathInput.UnregisterCallback<FocusEvent>(OnDockerfilePathInputFocus);
            _containerizeImageNameInput.UnregisterValueChangedCallback(OnContainerizeInputsChanged);
            _containerizeImageTagInput.UnregisterValueChangedCallback(OnContainerizeInputsChanged);

            _createAppNameInput.UnregisterValueChangedCallback(OnCreateAppNameInputChanged);
            _serverImageNameInput.UnregisterValueChangedCallback(OnCreateInputsChanged);
            _serverImageTagInput.UnregisterValueChangedCallback(OnCreateInputsChanged);

            _deployAppNameInput.UnregisterValueChangedCallback(OnDeployAppNameInputChanged);
            _deployAppVersionInput.UnregisterValueChangedCallback(OnDeployAppVersionInputChanged);
        }

        /// <summary>
        /// Register click actions, mostly from buttons: Need to -= unregistry them @ OnDisable()
        /// </summary>
        private void registerClickCallbacks()
        {
            _debugBtn.clickable.clicked += onDebugBtnClick;

            _apiTokenVerifyBtn.clickable.clicked += onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked += onApiTokenGetBtnClick;
            _edgegapSignInBtn.clickable.clicked += OnEdgegapSignInBtnClick;
            _signOutBtn.clickable.clicked += OnSignOutBtnClickAsync;
            _joinEdgegapDiscordBtn.clickable.clicked += OnDiscordBtnClick;

            _infoLinuxRequirementsBtn.clickable.clicked += OnLinuxInfoClick;
            _installLinuxRequirementsBtn.clickable.clicked += OnInstallLinuxBtnClick;
            _buildParamsBtn.clickable.clicked += OnOpenBuildParamsBtnClick;
            _serverBuildBtn.clickable.clicked += OnBuildServerBtnClick;

            _infoDockerRequirementsBtn.clickable.clicked += OnDockerInfoClick;
            _validateDockerRequirementsBtn.clickable.clicked += OnValidateDockerBtnClick;
            _buildPathResetBtn.clickable.clicked += OnResetBuildPathBtnClick;
            _dockerfilePathResetBtn.clickable.clicked += OnResetDockerfilePathBtnClick;
            _containerizeServerBtn.clickable.clicked += OnContainerizeBtnClickAsync;

            _createAppNameShowDropdownBtn.clickable.clicked += OnCreateAppNameDropdownClick;
            _uploadImageCreateAppBtn.clickable.clicked += OnUploadImageCreateAppBtnClickAsync;
            _portMappingLabelLink.clickable.clicked += OnPortsMappingLinkClick;
            _appInfoLabelLink.clickable.clicked += OnYourAppLinkClick;

            //_deployLimitLabelLink.clickable.clicked += OnDeployLimitLinkClick;
            _deployAppNameShowDropdownBtn.clickable.clicked += OnDeployAppNameDropdownClick;
            _deployAppVersionShowDropdownBtn.clickable.clicked += OnDeployAppVersionDropdownClick;
            _deployAppBtn.clickable.clicked += OnDeploymentCreateBtnClick;
            _stopLastDeployBtn.clickable.clicked += OnStopLastDeployClick;
            _discordHelpBtn.clickable.clicked += OnDiscordBtnClick;

            //_serverConnectLink.clickable.clicked += OnServerConnectLinkClick;
            _lobbyMatchmakerLabelLink.clickable.clicked += OnLobbyMatchmakerLinkClick;
            //_lifecycleManageLabelLink.clickable.clicked += OnScalingLifecycleLinkClick;
        }

        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// Should parity the opposite of registerClickEvents().
        /// </summary>
        private void unregisterClickEvents()
        {
            _debugBtn.clickable.clicked -= onDebugBtnClick;

            _apiTokenVerifyBtn.clickable.clicked -= onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked -= onApiTokenGetBtnClick;
            _edgegapSignInBtn.clickable.clicked -= OnEdgegapSignInBtnClick;
            _signOutBtn.clickable.clicked -= OnSignOutBtnClickAsync;
            _joinEdgegapDiscordBtn.clickable.clicked -= OnDiscordBtnClick;

            _infoLinuxRequirementsBtn.clickable.clicked -= OnLinuxInfoClick;
            _installLinuxRequirementsBtn.clickable.clicked -= OnInstallLinuxBtnClick;
            _buildParamsBtn.clickable.clicked -= OnOpenBuildParamsBtnClick;
            _serverBuildBtn.clickable.clicked -= OnBuildServerBtnClick;

            _infoDockerRequirementsBtn.clickable.clicked -= OnDockerInfoClick;
            _validateDockerRequirementsBtn.clickable.clicked -= OnValidateDockerBtnClick;
            _buildPathResetBtn.clickable.clicked -= OnResetBuildPathBtnClick;
            _dockerfilePathResetBtn.clickable.clicked -= OnResetDockerfilePathBtnClick;
            _containerizeServerBtn.clickable.clicked -= OnContainerizeBtnClickAsync;

            _createAppNameShowDropdownBtn.clickable.clicked -= OnCreateAppNameDropdownClick;
            _uploadImageCreateAppBtn.clickable.clicked -= OnUploadImageCreateAppBtnClickAsync;
            _portMappingLabelLink.clickable.clicked -= OnPortsMappingLinkClick;
            _appInfoLabelLink.clickable.clicked -= OnYourAppLinkClick;

            //_deployLimitLabelLink.clickable.clicked -= OnDeployLimitLinkClick;
            _deployAppNameShowDropdownBtn.clickable.clicked -= OnDeployAppNameDropdownClick;
            _deployAppVersionShowDropdownBtn.clickable.clicked -= OnDeployAppVersionDropdownClick;
            _deployAppBtn.clickable.clicked -= OnDeploymentCreateBtnClick;
            _stopLastDeployBtn.clickable.clicked -= OnStopLastDeployClick;
            _discordHelpBtn.clickable.clicked -= OnDiscordBtnClick;

            //_serverConnectLink.clickable.clicked -= OnServerConnectLinkClick;
            _lobbyMatchmakerLabelLink.clickable.clicked -= OnLobbyMatchmakerLinkClick;
            //_lifecycleManageLabelLink.clickable.clicked -= OnScalingLifecycleLinkClick;
        }

        private void initToggleDynamicUi()
        {
            hideResultLabels();
            loadPersistentDataFromEditorPrefs();

            _deployAppVersionShowDropdownBtn.SetEnabled(false);
            _stopLastDeployBtn.SetEnabled(false);
            _debugBtn.visible = EdgegapWindowMetadata.SHOW_DEBUG_BTN;
        }

        /// <summary>For example, result labels (success/err) should be hidden on init</summary>
        private void hideResultLabels()
        {
            _serverBuildResultLabel.visible = false;
            _containerizeServerResultLabel.visible = false;
            _deployResultLabel.style.display = DisplayStyle.None;
            _linuxRequirementsResultLabel.visible = false;
            _dockerRequirementsResultLabel.visible = false;
        }

        #region Init -> Button clicks
        /// <summary>
        /// Experiment here! You may want to log what you're doing
        /// in case you inadvertently leave it on.
        /// </summary>
        private void onDebugBtnClick() => debugEnableAllGroups();

        private void debugEnableAllGroups()
        {
            Debug.Log("debugEnableAllGroups");

            _serverBuildFoldout.SetEnabled(true);
            _containerizeFoldout.SetEnabled(true);
            _createAppFoldout.SetEnabled(true);
            _deployAppFoldout.SetEnabled(true);
            _nextStepsFoldout.SetEnabled(true);
        }

        private void onApiTokenVerifyBtnClick() => _ = verifyApiTokenGetRegistryCredsAsync();
        private void onApiTokenGetBtnClick() => OpenGetTokenUrl();

        /// <summary>
        /// "Sign in" btn click
        /// </summary>
        private void OnEdgegapSignInBtnClick() 
        { 
            OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_SIGN_IN_URL);
            ToggleIsConnectedConatiners(true);
        }

        /// <summary>
        /// "Sign out" btn click
        /// </summary>
        private void OnSignOutBtnClickAsync()
        {
            DeletePersistentDataFromEditorPrefs();
            ResetPluginValues();
            hideResultLabels();
            closeDisableGroups();
            ToggleIsConnectedConatiners(false);
        }

        private void OnLinuxInfoClick() => OpenEdgegapDocPageUrl(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH);

        private void OnDockerInfoClick() => OpenEdgegapDocPageUrl(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH);

        private void OnPortsMappingLinkClick() => OpenEdgegapDocPageUrl(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH);

        private void OnYourAppLinkClick() => OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_APP_INFO_URL);

        //TODO define url
        private void OnDeployLimitLinkClick() => OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_FREE_TIER_INFO_URL);

        //TODO define url
        private void OnServerConnectLinkClick() => OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_CONNECT_TO_SERVER_URL);

        private void OnLobbyMatchmakerLinkClick() => OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_LOBBY_MATCHMAKER_INFO_URL);

        //TODO define url
        private void OnScalingLifecycleLinkClick() => OpenWebsiteUrl(EdgegapWindowMetadata.SCALING_LIFECYCLE_INFO_URL);

        private void OnDiscordBtnClick() => OpenWebsiteUrl(EdgegapWindowMetadata.EDGEGAP_DISCORD_URL);

        /// <summary>
        /// Linux server build requirements install btn click
        /// Only manual module install is supported for now, so => open docs
        /// </summary>
        private /*async*/ void OnInstallLinuxBtnClick() 
        {
            //await InstallLinuxRequirements();

            OpenEdgegapDocPageUrl(EdgegapWindowMetadata.EDGEGAP_DOC_PLUGIN_GUIDE_PATH, "#install-unity-linux-build-support");
        }

        /// <summary>
        /// Open Unity build settings btn click
        /// </summary>
        private void OnOpenBuildParamsBtnClick()
        {
#if UNITY_2021_3_OR_NEWER
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
#else
            EditorApplication.ExecuteMenuItem("File/Build Settings...");
#endif
        }

        /// <summary>
        /// Validate Docker installation btn click
        /// </summary>
        private async void OnValidateDockerBtnClick()
        {
            _validateDockerRequirementsBtn.SetEnabled(false);
            hideResultLabels();

            try { await ValidateDockerRequirementsAsync(); }
            finally
            {
                _validateDockerRequirementsBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// Reset Build Path input value btn click
        /// </summary>
        private void OnResetBuildPathBtnClick()
        {
            _buildPathInput.value = "";
        }

        /// <summary>
        /// Reset Dockerfile Path input value btn click
        /// </summary>
        private void OnResetDockerfilePathBtnClick()
        {
            _dockerfilePathInput.value = "";
        }

        /// <summary>
        /// "Build server" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private void OnBuildServerBtnClick()
        {
            try
            {
                _serverBuildBtn.SetEnabled(false);
                hideResultLabels();

                BuildServer();

                _containerizeFoldout.value = true;
                _buildPathInput.value = ProjectRootPath + "/Builds/" + _buildFolderNameInput.value;
            }
            catch (Exception e)
            {
                Debug.LogError($"OnBuildServerBtnClick Error: {e}");
                OnBuildContainerizeUploadError(e.Message, _serverBuildResultLabel, "Build failed (see logs).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _serverBuildBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// "Containerize with Docker" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnContainerizeBtnClickAsync()
        {
            try
            {
                _containerizeServerBtn.SetEnabled(false);
                _apiTokenVerifyBtn.SetEnabled(false);
                _signOutBtn.SetEnabled(false);
                hideResultLabels();

                await BuildDockerImageAsync();

                _createAppFoldout.value = true;
                _serverImageNameInput.value = _containerizeImageNameInput.value;
                _serverImageTagInput.value = _containerizeImageTagInput.value;
            }
            catch (Exception e)
            {
                Debug.LogError($"OnContainerizeBtnClick Error: {e}");
                OnBuildContainerizeUploadError(e.Message, _containerizeServerResultLabel, "Containerization failed (see logs).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _containerizeServerBtn.SetEnabled(true);
                _apiTokenVerifyBtn.SetEnabled(true);
                _signOutBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// Show app name dropdown (Create App section) btn click
        /// </summary>
        private void OnCreateAppNameDropdownClick()
        {
            List<string> appNameBtns = !(_existingAppNames is null) || _existingAppNames.Count > 0 ? 
                _existingAppNames.Prepend("Create New Application").ToList() : new List<string> { "Create New Application" };

            UnityEditor.PopupWindow.Show(_createAppNameShowDropdownBtn.worldBound,
                new CustomPopupContent(appNameBtns, OnDropdownCreateAppNameSelect));
        }

        /// <summary>
        /// Show app name dropdown (Deploy section) btn click 
        /// </summary>
        private void OnDeployAppNameDropdownClick()
        {
            UnityEditor.PopupWindow.Show(_deployAppNameShowDropdownBtn.worldBound,
                new CustomPopupContent(_existingAppNames, OnDropdownDeployAppNameSelect));
        }

        /// <summary>
        /// Show app version dropdown btn click
        /// </summary>
        private void OnDeployAppVersionDropdownClick()
        {
            UnityEditor.PopupWindow.Show(_deployAppVersionShowDropdownBtn.worldBound,
                new CustomPopupContent(_storedDeployAppVersions, OnDropDownDeployAppVersionSelect));
        }

        /// <summary>
        /// Select an app from the Create App section dropdown btn click
        /// </summary>
        /// <param name="name"></param>
        private void OnDropdownCreateAppNameSelect(string name)
        {
            string appName = Regex.Replace(name, @"\s", "_");
            _createAppNameInput.value = appName;
        }

        /// <summary>
        /// Select an app from the Deploy section dropdown btn click
        /// </summary>
        /// <param name="name"></param>
        private void OnDropdownDeployAppNameSelect(string name)
        {
            string appName = Regex.Replace(name, @"\s", "_");
            _deployAppNameInput.value = appName;
        }

        /// <summary>
        /// Select an app version from the Deploy section dropdown btn click
        /// </summary>
        /// <param name="version"></param>
        private void OnDropDownDeployAppVersionSelect(string version)
        {
            _deployAppVersionInput.value = version;
        }

        /// <summary>
        /// "Upload image and Create app version" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnUploadImageCreateAppBtnClickAsync()
        {
            try
            {
                _uploadImageCreateAppBtn.SetEnabled(false);
                _apiTokenVerifyBtn.SetEnabled(false);
                _signOutBtn.SetEnabled(false);
                hideResultLabels();

                await UploadImageAsync();

                ShowWorkInProgress("Create Application", "Updating server info on Edgegap");
                await CreateAppAsync();

                OpenCreateAppVersionUrl();
                
                _deployAppFoldout.value = true;
                _deployAppNameInput.value = _createAppNameInput.value;
                _deployAppNameShowDropdownBtn.SetEnabled(true);                
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Docker authorization failed") || e.Message.Contains("Unable to push docker image"))
                {
                    OnBuildContainerizeUploadError(e.Message);
                }
                else
                {
                    Debug.LogError($"OnUploadImageCreateAppBtnClick Error: {e}");
                    OnBuildContainerizeUploadError("Image upload and app creation failed (see logs).");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _uploadImageCreateAppBtn.SetEnabled(true);
                _apiTokenVerifyBtn.SetEnabled(true);
                _signOutBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// "Deploy to cloud" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnDeploymentCreateBtnClick() 
        {
            try
            {
                _apiTokenVerifyBtn.SetEnabled(false);
                _signOutBtn.SetEnabled(false);
                _stopLastDeployBtn.SetEnabled(false);
                _deployAppBtn.SetEnabled(false);
                hideResultLabels();

                await CreateDeploymentStartServerAsync();

                _deployAppBtn.SetEnabled(false);
                _nextStepsFoldout.value = true;
            }
            catch (Exception e)
            {
                OnCreateDeploymentStartServerFail(false, e.Message);
                _deployAppBtn.SetEnabled(CheckFilledDeployServerInputs());
            }
            finally
            {
                _apiTokenVerifyBtn.SetEnabled(true);
                _signOutBtn.SetEnabled(true);
                _stopLastDeployBtn.SetEnabled(!string.IsNullOrEmpty(_deploymentRequestId));
            }
        }

        /// <summary>
        /// "Stop last deployment" btn click
        /// Process UI + validation before/after API logic
        /// </summary>
        private async void OnStopLastDeployClick()
        {
            try
            {
                _apiTokenVerifyBtn.SetEnabled(false);
                _signOutBtn.SetEnabled(false);
                _stopLastDeployBtn.SetEnabled(false);
                _deployAppBtn.SetEnabled(false);
                hideResultLabels();

                await StopDeploymentAsync();

                _nextStepsFoldout.value = true;
            }
            catch (Exception e)
            {
                OnGetStopLastDeploymentFail(e.Message);
            }
            finally
            {
                _apiTokenVerifyBtn.SetEnabled(true);
                _signOutBtn.SetEnabled(true);
                _stopLastDeployBtn.SetEnabled(false);
                _deployAppBtn.SetEnabled(CheckFilledDeployServerInputs());
            }
        }
        #endregion // Init -> /Button Clicks
        #endregion // Init

        #region Filled Inputs Checkers
        private bool CheckFilledContainerizeServerInputs()
        {
            return _containerizeImageNameInput.value.Length > 0 
                && _containerizeImageTagInput.value.Length > 0;
        }

        private bool CheckAnyContainerizeServerInput()
        {
            return _buildPathInput.value.Length > 0
                || _containerizeImageNameInput.value.Length > 0
                || _containerizeImageTagInput.value.Length > 0
                || _dockerfilePathInput.value.Length > 0
                || _optionalDockerParamsInput.value.Length > 0;
        }

        private bool CheckFilledCreateAppInputs()
        {
            return _createAppNameInput.value.Length > 0
                && _serverImageNameInput.value.Length > 0
                && _serverImageTagInput.value.Length > 0;
        }

        private bool CheckAnyCreateAppInput()
        {
            return _createAppNameInput.value.Length > 0
                || _serverImageNameInput.value.Length > 0
                || _serverImageTagInput.value.Length > 0;
        }

        private bool CheckFilledDeployServerInputs()
        {
            return _deployAppNameInput.value.Length > 0
                && _deployAppVersionInput.value.Length > 0;
        }

        private bool CheckAnyDeployServerInput()
        {
            return _deployAppNameInput.value.Length > 0
                || _deployAppVersionInput.value.Length > 0;
        }
        #endregion //Filled Inputs Checkers

        /// <summary>TODO: Load persistent data?</summary>
        private void syncFormWithObjectStatic()
        {
            // Only show the rest of the form if apiToken is verified
            _postAuthContainer.SetEnabled(_isApiTokenVerified);

            // Only enable certain elements if the required inputs are filled
            bool containerizeServerInputsFilled = CheckFilledContainerizeServerInputs();
            _containerizeServerBtn.SetEnabled(containerizeServerInputsFilled);

            bool createAppInputsFilled = CheckFilledCreateAppInputs();
            _uploadImageCreateAppBtn.SetEnabled(createAppInputsFilled);

            bool deployServerInputsFilled = CheckFilledDeployServerInputs();
            _deployAppBtn.SetEnabled(deployServerInputsFilled);
        }

        /// <summary>
        /// Dynamically set form based on API call results.
        /// => If APIToken is cached via EditorPrefs, verify => gets registry creds + applications.
        /// => If deployAppName is cached via ViewDataKey, loads the app versions + check if enable stop btn.
        /// </summary>
        private async Task syncFormWithObjectDynamicAsync()
        {
            if (string.IsNullOrEmpty(_apiTokenInput.value))
            {
                //show Sign In btn
                ToggleIsConnectedConatiners(false);
                return;
            }
            else
            {
                //show API Token field/btns + Sign Out btn
                ToggleIsConnectedConatiners(true);
            }   

            // We found a cached api token: Verify =>
            if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken; " +
                "calling verifyApiTokenGetRegistryCredsAsync =>");
            await verifyApiTokenGetRegistryCredsAsync();

            // Was the API token verified? Load the applications for the dropdown
            if (_isApiTokenVerified)
            {
                if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken; " +
                "calling GetAppsAsync =>");
                await GetAppsAsync();
            }

            // Was the API token verified + we found a cached appName in Deploy section? Load the app versions for the dropdown =>
            // But ignore errs, since we're just *assuming* the app exists since the appName was filled
            if (_isApiTokenVerified && _deployAppNameInput.value.Length > 0)
            {
                if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken && deployAppName value; " +
                "calling GetAppVersionsAsync =>");

                try { await GetAppVersionsAsync(); }
                finally
                {
                    if (_storedDeployAppVersions is not null && _storedDeployAppVersions.Count > 0)
                    {
                        _deployAppVersionShowDropdownBtn.SetEnabled(true);
                    }
                }
            }

            // Was the API token verified + there are filled inputs in Deploy section? Check if we enable start/stop deployment btn
            if (_isApiTokenVerified && CheckAnyDeployServerInput())
            {
                _deployAppBtn.SetEnabled(string.IsNullOrEmpty(_deploymentRequestId));
                _stopLastDeployBtn.SetEnabled(!string.IsNullOrEmpty(_deploymentRequestId));
            }
        }

        /// <summary>
        /// Change between the view when connected or the view when not connected
        /// </summary>
        /// <param name="isConnected"></param>
        private void ToggleIsConnectedConatiners(bool isConnected)
        {
            _connectedContainer.style.display = isConnected ? DisplayStyle.Flex : DisplayStyle.None;
            _connectedContainer.SetEnabled(isConnected);

            _signInContainer.style.display = isConnected? DisplayStyle.None : DisplayStyle.Flex;
            _signInContainer.SetEnabled(!isConnected); 
        }


#region Immediate non-button changes
        /// <summary>
        /// When field gains focus, open File Explorer to select folder path
        /// </summary>
        /// <param name="evt"></param>
        private void OnBuildPathInputFocus(FocusEvent evt)
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Build Folder", ProjectRootPath, "");

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.Contains(ProjectRootPath.Replace('\\', '/')))
                {
                    string pathFromProjectRoot = selectedPath.Split(ProjectRootPath.Replace('\\', '/') + '/')[1];
                    _buildPathInput.value = pathFromProjectRoot;
                }
                else
                {
                    OnBuildContainerizeUploadError("The selected build folder couldn't be found within the project.");
                }
            }
        }

        /// <summary>
        /// When field gains focus, open File Explorer to select file path
        /// </summary>
        /// <param name="evt"></param>
        private void OnDockerfilePathInputFocus(FocusEvent evt)
        {
            string selectedPath = EditorUtility.OpenFilePanel("Select Dockerfile", ProjectRootPath, "");

            if (!string.IsNullOrEmpty(selectedPath))
            {
                _dockerfilePathInput.value = selectedPath;
            }
        }

        /// <summary>
        /// On change, toggle containerize btn if all required inputs in Containerize section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnContainerizeInputsChanged(ChangeEvent<string> evt)
        {
            bool requiredInputsFilled = CheckFilledContainerizeServerInputs();
            _containerizeServerBtn.SetEnabled(requiredInputsFilled);
        }

        /// <summary>
        /// On change, validate
        /// Toggle create app btn if all required inputs are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnCreateAppNameInputChanged(ChangeEvent<string> evt)
        {
            // Validate: Only allow alphanumeric, underscore, dash, plus, period
            if (!_appNameAllowedCharsRegex.IsMatch(evt.newValue))
                _createAppNameInput.value = evt.previousValue; // Revert to the previous value

            bool requiredInputsFilled = CheckFilledCreateAppInputs();
            _uploadImageCreateAppBtn.SetEnabled(requiredInputsFilled);
        }

        /// <summary>
        /// On change, toggle create app btn if all required inputs in Create App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnCreateInputsChanged(ChangeEvent<string> evt)
        {
            bool requiredInputsFilled = CheckFilledCreateAppInputs();
            _uploadImageCreateAppBtn.SetEnabled(requiredInputsFilled);
        }

        /// <summary>
        /// On change, validate
        /// Toggle deploy app btn if all required inputs in Deploy App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private async void OnDeployAppNameInputChanged(ChangeEvent<string> evt)
        {
            // Validate: Only allow alphanumeric, underscore, dash, plus, period
            if (!_appNameAllowedCharsRegex.IsMatch(evt.newValue))
                _deployAppNameInput.value = evt.previousValue; // Revert to the previous value

            bool requiredInputsFilled = CheckFilledDeployServerInputs();
            _deployAppBtn.SetEnabled(requiredInputsFilled);

            if (IsInitd && _isApiTokenVerified)
            {
                try
                {
                    if (_storedDeployAppVersions is not null)
                    {
                        _storedDeployAppVersions.Clear();
                    }

                    _deployAppVersionShowDropdownBtn.SetEnabled(false);

                    if (_existingAppNames.Contains(evt.newValue))
                    {
                        _apiTokenVerifyBtn.SetEnabled(false);
                        _signOutBtn.SetEnabled(false);
                        await GetAppVersionsAsync();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"GetAppVersions Error: {e}");
                }
                finally
                {
                    _apiTokenVerifyBtn.SetEnabled(true);
                    _signOutBtn.SetEnabled(true);

                    if (_storedDeployAppVersions is not null && _storedDeployAppVersions.Count > 0)
                    {
                        _deployAppVersionShowDropdownBtn.SetEnabled(true);
                    }
                }
            }
        }

        /// <summary>
        /// On change, toggle deploy app btn if all required inputs in Deploy App section are filled
        /// </summary>
        /// <param name="evt"></param>
        private void OnDeployAppVersionInputChanged(ChangeEvent<string> evt)
        {
            bool requiredInputsFilled = CheckFilledDeployServerInputs();
            _deployAppBtn.SetEnabled(requiredInputsFilled);
        }

        /// <summary>
        /// While changing the token, we temporarily unmask. On change, set state to !verified.
        /// </summary>
        /// <param name="evt"></param>
        private void onApiTokenInputChanged(ChangeEvent<string> evt)
        {
            // Unmask while changing
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = false;

            // Token changed? Reset form to !verified state and fold all groups
            _isApiTokenVerified = false;
            _postAuthContainer.SetEnabled(false);
            closeDisableGroups();

            // Toggle "Verify" btn on 1+ char entered
            _apiTokenVerifyBtn.SetEnabled(evt.newValue.Length > 0);
        }

        /// <summary>Unmask while typing</summary>
        /// <param name="evt"></param>
        private void onApiTokenInputFocusOut(FocusOutEvent evt)
        {
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = true;
        }
#endregion // Immediate non-button changes

        /// <summary>
        /// Verifies token => apps/container groups -> gets registry creds (if any).
        /// TODO: UX - Show loading spinner.
        /// </summary>
        private async Task verifyApiTokenGetRegistryCredsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("verifyApiTokenGetRegistryCredsAsync");

            // Disable most ui while we verify
            _isApiTokenVerified = false;
            _apiTokenVerifyBtn.SetEnabled(false);
            _signOutBtn.SetEnabled(false);
            SyncContainerEnablesToState();
            hideResultLabels();

            EdgegapWizardApi wizardApi = getWizardApi();
            EdgegapHttpResult initQuickStartResultCode = await wizardApi.InitQuickStart();

            _apiTokenVerifyBtn.SetEnabled(true);
            _signOutBtn.SetEnabled(true);
            _isApiTokenVerified = initQuickStartResultCode.IsResultCode204;

            if (!_isApiTokenVerified)
            {
                SyncContainerEnablesToState();
                return;
            }

            // Verified: Let's see if we have active registry credentials // TODO: This will later be a result model
            EdgegapHttpResult<GetRegistryCredentialsResult> getRegistryCredentialsResult = await wizardApi.GetRegistryCredentials();

            if (getRegistryCredentialsResult.IsResultCode200)
            {
                // Success
                _credentials = getRegistryCredentialsResult.Data;
                persistUnmaskedApiToken(_apiTokenInput.value);
                SetContainerRegistryData(_credentials);
            }
            else
            {
                // Fail
            }

            // Unlock the rest of the form, whether we prefill the container registry or not
            SyncContainerEnablesToState();
        }

        /// <summary>
        /// We have container registry params; we'll store the registry container fields.
        /// </summary>
        /// <param name="credentials">GetRegistryCredentialsResult</param>
        private void SetContainerRegistryData(GetRegistryCredentialsResult credentials)
        {
            if (IsLogLevelDebug) Debug.Log("SetContainerRegistryData");

            if (credentials == null)
                throw new Exception($"!{nameof(credentials)}");

            _containerRegistryUrl = credentials.RegistryUrl;
            _containerProject = _credentials.Project;
            _containerUsername = credentials.Username;
            _containerToken = credentials.Token;
        }

        public static string Base64Encode(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainBytes);
        }

        public static string Base64Decode(string base64EncodedText)
        {
            byte[] base64Bytes = Convert.FromBase64String(base64EncodedText);
            return Encoding.UTF8.GetString(base64Bytes);
        }

        /// <summary>
        /// Toggle container groups and foldouts on/off based on:
        /// - _isApiTokenVerified
        /// </summary>
        private void SyncContainerEnablesToState()
        {
            // Requires _isApiTokenVerified
            _postAuthContainer.SetEnabled(_isApiTokenVerified); // Entire body container
            _serverBuildFoldout.SetEnabled(_isApiTokenVerified);
            _containerizeFoldout.SetEnabled(_isApiTokenVerified);
            _createAppFoldout.SetEnabled(_isApiTokenVerified);
            _deployAppFoldout.SetEnabled(_isApiTokenVerified);
            _nextStepsFoldout.SetEnabled(_isApiTokenVerified);  

            if (!_isApiTokenVerified)
            {
                _serverBuildFoldout.value = false;
                _containerizeFoldout.value = false;
                _createAppFoldout.value = false;
                _deployAppFoldout.value = false;
                _nextStepsFoldout.value = false;
            }
            else
            {
                _serverBuildFoldout.value = true;

                //open other foldouts if persistent data is found in inputs
                if (CheckAnyContainerizeServerInput())
                    _containerizeFoldout.value = true;

                if (CheckAnyCreateAppInput())
                    _createAppFoldout.value = true;

                if (CheckAnyDeployServerInput())
                    _deployAppFoldout.value = true;
            }
        }


        private void OpenWebsiteUrl(string url)
        {
            Application.OpenURL(url + "?" + EdgegapWindowMetadata.DEFAULT_UTM_TAGS);
        }

        private void OpenGetTokenUrl()
        {
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_GET_A_TOKEN_URL + "&" + EdgegapWindowMetadata.DEFAULT_UTM_TAGS);
        }

        private void OpenCreateAppVersionUrl()
        {
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_CREATE_APP_BASE_URL + _createAppNameInput.value + 
                                "/versions/create/?" + EdgegapWindowMetadata.DEFAULT_UTM_TAGS);
        }

        /// <summary>
        /// Open Edgegap documentation at a specific page/section
        /// </summary>
        /// <param name="pagePath">path to specific doc page</param>
        /// <param name="pageSection">null || which section of the page to jump to</param>
        private void OpenEdgegapDocPageUrl(string pagePath, string pageSection = null)
        {
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_DOC_BASE_URL + pagePath + "?" +
                                EdgegapWindowMetadata.DEFAULT_UTM_TAGS + pageSection ?? "");
        }

        /// <summary>
        /// Retrieve and store existing apps
        /// </summary>
        /// <returns></returns>
        private async Task GetAppsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("GetAppsAsync");

            _createAppNameShowDropdownBtn.SetEnabled(false);
            _deployAppNameShowDropdownBtn.SetEnabled(false);

            EdgegapAppApi appApi = getAppApi();
            EdgegapHttpResult<GetAppsResult> getAppsResult = await appApi.GetApps();

            if (getAppsResult.IsResultCode200)
            {
                GetAppsResult existingApps = getAppsResult.Data;
                _existingAppNames = existingApps.Applications.Select(app => app.AppName).ToList();
            }

            _createAppNameShowDropdownBtn.SetEnabled(true);

            if (_existingAppNames.Count > 0)
            {
                _deployAppNameShowDropdownBtn.SetEnabled(true);
            }
        }

        /// <summary>
        /// Retrieve and store an app's versions
        /// </summary>
        /// <returns></returns>
        private async Task GetAppVersionsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("GetAppVersionsAsync");

            EdgegapAppApi appApi = getAppApi();
            EdgegapHttpResult<GetAppVersionsResult> getAppVersionsResult = await appApi.GetAppVersions(_deployAppNameInput.value);

            if (getAppVersionsResult.IsResultCode200)
            {
                GetAppVersionsResult appVersionsData = getAppVersionsResult.Data;

                List<VersionData> activeVersions = appVersionsData.Versions.Where(version => version.IsActive).ToList();
                _storedDeployAppVersions = activeVersions.Select(version => version.Name).ToList();
            }
            else
            {
                Debug.LogWarning($"Unable to retrieve app versions for application {_deployAppNameInput.value}.\n" +
                    $"Status {getAppVersionsResult.StatusCode}: {getAppVersionsResult.ReasonPhrase}");
            }
        }

        /// <summary>
        /// TODO: Add err handling for reaching app limit (max 2 for free tier).
        /// </summary>
        private async Task CreateAppAsync()
        {
            if (IsLogLevelDebug) Debug.Log("createAppAsync");

            EdgegapAppApi appApi = getAppApi();
            CreateAppRequest createAppRequest = new CreateAppRequest( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                _createAppNameInput.value,
                isActive: true,
                "");

            EdgegapHttpResult<GetCreateAppResult> createAppResult = await appApi.CreateApp(createAppRequest);

            if (!(createAppResult.IsResultCode200 || createAppResult.IsResultCode409)) // 409 == app already exists
            {
                Debug.Log(createAppResult.HasErr);
                throw new Exception($"Error {createAppResult.StatusCode}: {createAppResult.ReasonPhrase}");
            }
            else if (!_existingAppNames.Contains(_createAppNameInput.value))
            {
                _existingAppNames.Add(_createAppNameInput.value);
            }
        }

        /// <summary>Slight animation shake</summary>
        private void shakeNeedMoreGameServersBtn()
        {
            ButtonShaker shaker = new ButtonShaker(_deployLimitLabelLink);
            _ = shaker.ApplyShakeAsync();
        }

        private void openDocumentationWebsite()
        {
            string documentationUrl = _apiEnvironment.GetDocumentationUrl() + "?" +
                                      EdgegapWindowMetadata.DEFAULT_UTM_TAGS;

            if (!string.IsNullOrEmpty(documentationUrl))
                Application.OpenURL(documentationUrl);
            else
            {
                string apiEnvName = Enum.GetName(typeof(ApiEnvironment), _apiEnvironment);
                Debug.LogWarning($"Could not open documentation for api environment " +
                    $"{apiEnvName}: No documentation URL.");
            }
        }

        /// <summary>
        /// Starts a new deployment & waits for it to be READY
        /// </summary>
        private async Task CreateDeploymentStartServerAsync()
        {
            if (IsLogLevelDebug) Debug.Log("createDeploymentStartServerAsync");
            
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.DEPLOY_REQUEST_RICH_STR,
                EdgegapWindowMetadata.StatusColors.Processing);
            _deployResultLabel.style.display = DisplayStyle.Flex;

            EdgegapDeploymentsApi deployApi = getDeployApi();

            // Get (+cache) external IP async, required to create a deployment. Prioritize cache.
            _userExternalIp = await getExternalIpAddress();

            CreateDeploymentRequest createDeploymentReq = new CreateDeploymentRequest( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                _deployAppNameInput.value,
                _deployAppVersionInput.value,
                _userExternalIp);

            // Request to deploy (it won't be active, yet) =>
            EdgegapHttpResult<CreateDeploymentResult> createDeploymentResponse =
                await deployApi.CreateDeploymentAsync(createDeploymentReq);

            if (!createDeploymentResponse.IsResultCode200)
            {
                OnCreateDeploymentStartServerFail(createDeploymentResponse.IsResultCode403, createDeploymentResponse.Error.ErrorMessage);
                return;
            }
            else
            {
                // Update status
                _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "<i>Deploying...</i>", EdgegapWindowMetadata.StatusColors.Processing);
            }

            // Check the status of the deployment for READY every 2s =>
            const int pollIntervalSecs = EdgegapWindowMetadata.DEPLOYMENT_READY_STATUS_POLL_SECONDS;
            EdgegapHttpResult<GetDeploymentStatusResult> getDeploymentStatusResponse = await deployApi.AwaitReadyStatusAsync(
                createDeploymentResponse.Data.RequestId,
                TimeSpan.FromSeconds(pollIntervalSecs));

            // Process create deployment response
            bool isSuccess = getDeploymentStatusResponse.IsResultCode200;
            if (isSuccess)
                OnCreateDeploymentOrRefreshSuccess(getDeploymentStatusResponse.Data);
            else
                OnCreateDeploymentStartServerFail(getDeploymentStatusResponse.IsResultCode403, getDeploymentStatusResponse.Error.ErrorMessage); //403 = Maximum number of deployments reached
        }

        /// <summary>
        /// CreateDeployment || RefreshDeployment success handler.
        /// </summary>
        /// <param name="getDeploymentStatusResult">Only pass from CreateDeployment</param>
        private void OnCreateDeploymentOrRefreshSuccess(GetDeploymentStatusResult getDeploymentStatusResult)
        {
            // Success
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "Server deployed successfully. Don't forget to remove the deployment after testing.",
                EdgegapWindowMetadata.StatusColors.Success);
            _deployResultLabel.style.display = DisplayStyle.Flex;

            // Cache the deployment result -> persist the requestId
            _deploymentRequestId = getDeploymentStatusResult.RequestId;
            EditorPrefs.SetString(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR, _deploymentRequestId);
        }

        private void OnCreateDeploymentStartServerFail(bool reachedNumDeploymentsHardcap, string message = null)
        {
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "There was an issue, see Unity console for details.",
                EdgegapWindowMetadata.StatusColors.Error);

            _deployResultLabel.style.display = DisplayStyle.Flex;
            Debug.LogError(message ?? "Unknown Error");
            Debug.Log("(!) Check your deployments here: https://app.edgegap.com/deployment-management/deployments/list");

            // Shake "Free Tier deployment limit" btn on maximum number of deployments reached
            if (reachedNumDeploymentsHardcap)
                shakeNeedMoreGameServersBtn();
        }

        /// <summary>
        /// Stop currently stored deployment
        /// </summary>
        /// <returns></returns>
        private async Task StopDeploymentAsync()
        {
            if (IsLogLevelDebug) Debug.Log("GetStopLastDeploymentAsync");

            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                    "<i>Stopping...</i>", EdgegapWindowMetadata.StatusColors.Warn);
            _deployResultLabel.style.display = DisplayStyle.Flex;

            //Stop request
            EdgegapDeploymentsApi deployApi = getDeployApi();
            EdgegapHttpResult<StopActiveDeploymentResult> stopDeploymentResponse = await deployApi.StopActiveDeploymentAsync(_deploymentRequestId);

            if (!stopDeploymentResponse.IsResultCode200)
            {
                OnGetStopLastDeploymentFail(stopDeploymentResponse.Error.ErrorMessage);
                return;
            }

            //Check status of deployment for STOPPED every 2s
            TimeSpan pollIntervalSecs = TimeSpan.FromSeconds(EdgegapWindowMetadata.DEPLOYMENT_STOP_STATUS_POLL_SECONDS);
            stopDeploymentResponse = await deployApi.AwaitTerminatedDeleteStatusAsync(_deploymentRequestId, pollIntervalSecs);

            //Process response
            if (!stopDeploymentResponse.IsResultCode410)
            {
                OnGetStopLastDeploymentFail(stopDeploymentResponse.Error.ErrorMessage);
            }
            else
            {
                OnGetStopLastDeploymentSuccess();
            }
        }

        private void OnGetStopLastDeploymentSuccess()
        {
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "Deployment stopped successfully",
                EdgegapWindowMetadata.StatusColors.Success);
            _deployResultLabel.style.display = DisplayStyle.Flex;
            _deploymentRequestId = "";
            EditorPrefs.DeleteKey(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR);
        }

        private void OnGetStopLastDeploymentFail(string message)
        {
            _deployResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                "There was an issue, see Unity console for details.",
                EdgegapWindowMetadata.StatusColors.Error);

            _deployResultLabel.style.display = DisplayStyle.Flex;
            Debug.LogError(message);
        }

        /// <summary>Sets and returns `_userExternalIp`, prioritizing local cache</summary>
        private async Task<string> getExternalIpAddress()
        {
            if (!string.IsNullOrEmpty(_userExternalIp))
                return _userExternalIp;

            EdgegapIpApi ipApi = getIpApi();
            EdgegapHttpResult<GetYourPublicIpResult> getYourPublicIpResponseTask = await ipApi.GetYourPublicIp();

            _userExternalIp = getYourPublicIpResponseTask?.Data?.PublicIp;
            Assert.IsTrue(!string.IsNullOrEmpty(_userExternalIp),
                $"Expected getYourPublicIpResponseTask.Data.PublicIp");

            return _userExternalIp;
        }

#region Api Builders
        private EdgegapDeploymentsApi getDeployApi() => new EdgegapDeploymentsApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapIpApi getIpApi() => new EdgegapIpApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapWizardApi getWizardApi() => new EdgegapWizardApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);

        private EdgegapAppApi getAppApi() => new EdgegapAppApi( // MIRROR CHANGE: 'new()' not supported in Unity 2020
            EdgegapWindowMetadata.API_ENVIRONMENT,
            _apiTokenInput.value.Trim(),
            EdgegapWindowMetadata.LOG_LEVEL);
#endregion // Api Builders


        private float ProgressCounter = 0;

        // MIRROR CHANGE: added title parameter for more detailed progress while waiting
        void ShowWorkInProgress(string title, string status)
        {
            EditorUtility.DisplayProgressBar(title, status, ProgressCounter++ / 50);
        }
        // END MIRROR CHANGE

        /// <summary>
        /// Install Linux build support modules via the command line
        /// currently unused because of how Unity executes this command
        /// </summary>
        /// <returns></returns>
        private async Task InstallLinuxRequirements()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                ProgressCounter = 0;

                await EdgegapBuildUtils.InstallLinuxModules(Application.unityVersion,
                    outputReciever: status => ShowWorkInProgress("Installing linux support modules", status),
                    errorReciever: (msg) => OnBuildContainerizeUploadError(msg, _linuxRequirementsResultLabel, "There was a problem.")
                );

                OnBuildContainerizeUploadSuccess(_linuxRequirementsResultLabel, "Requirements installed. Don't forget to restart Unity.");
            }
            else
            {
                OnBuildContainerizeUploadSuccess(_linuxRequirementsResultLabel, "Requirements installed.");
            }
        }

        /// <summary>
        /// Check if Docker is installed and currently running
        /// </summary>
        /// <returns></returns>
        private async Task ValidateDockerRequirementsAsync()
        {
            (bool isValid, string error) = await EdgegapBuildUtils.DockerSetupAndInstallationCheck(DefaultDockerFilePath);

            if (!isValid)
            {
                OnBuildContainerizeUploadError(error.Contains("docker daemon is not running") || error.Contains("dockerDesktop") ?
                        "Docker is installed, but the daemon/app (such as `Docker Desktop`) is not running. " +
                            "Please start Docker Desktop and try again." :
                        "Docker installation not found. Docker can be downloaded from:\n\nhttps://www.docker.com/",
                    _dockerRequirementsResultLabel, "There was a problem."
                    );
            }
            else
            {
                OnBuildContainerizeUploadSuccess(_dockerRequirementsResultLabel, "Docker is running.");
            }
        }

        /// <summary>
        /// Create server build
        /// </summary>
        private void BuildServer()
        {
            if (IsLogLevelDebug) Debug.Log("buildServer");       
            ProgressCounter = 0;

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                throw new Exception(
                    $"Linux Build Support is missing.\n\nPlease install it via the plugin, or open Unity Hub -> Installs -> Unity {Application.unityVersion} -> Add Modules -> Linux Build Support (IL2CPP & Mono & Dedicated Server) -> Install\n\nAfterwards restart Unity!"
                    );
            }

            string folderName = string.IsNullOrEmpty(_buildFolderNameInput.value) ?
                _buildFolderNameInput.value : DefaultFolderName;

            BuildReport buildResult = EdgegapBuildUtils.BuildServer(folderName);
            if (buildResult.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new Exception();
            }
            else
            {
                OnBuildContainerizeUploadSuccess(_serverBuildResultLabel, "Build succeeded.");
            }

            OnBuildContainerizeUploadSuccess(_serverBuildResultLabel, "Build succeeded.");
        }

        /// <summary>
        /// Create server Docker image
        /// </summary>
        /// <returns></returns>
        private async Task BuildDockerImageAsync()
        {
            if (IsLogLevelDebug) Debug.Log("buildDockerImageAsync");
            ProgressCounter = 0;

            string dockerfilePath = _dockerfilePathInput.value.Length > 0 ? 
                _dockerfilePathInput.value : DefaultDockerFilePath;

            string extraParams = _optionalDockerParamsInput.value.Length > 0 ?
                _optionalDockerParamsInput.value : null;

            if (_buildPathInput.value.Length > 0)
            {
                if (string.IsNullOrEmpty(extraParams))
                {
                    extraParams = $"--build-arg SERVER_BUILD_PATH=\"{_buildPathInput.value}\"";
                }
                else
                {
                    extraParams += $" --build-arg SERVER_BUILD_PATH=\"{_buildPathInput.value}\"";
                }
            }

            string imageName = _containerizeImageNameInput.value.ToLowerInvariant();
            string imageRepo = $"{_containerProject}/{imageName}";
            string tag = _containerizeImageTagInput.value;

            await EdgegapBuildUtils.RunCommand_DockerBuild(
                dockerfilePath, _containerRegistryUrl, imageRepo, tag, ProjectRootPath,
                status => ShowWorkInProgress("Building Docker Image", status),
                extraParams ?? null);

            OnBuildContainerizeUploadSuccess(_containerizeServerResultLabel, "Containerization succeeded.");
        }

        /// <summary>
        /// Upload server Docker image to Edgegap registry
        /// </summary>
        /// <returns></returns>
        private async Task UploadImageAsync()
        {
            if (IsLogLevelDebug) Debug.Log("uploadDockerImageAsync");
            ProgressCounter = 0;

            string imageName = _serverImageNameInput.value.ToLowerInvariant();
            string imageRepo = $"{_containerProject}/{imageName}";
            string tag = _containerizeImageTagInput.value;

            bool isDockerLoginSuccess = await EdgegapBuildUtils.LoginContainerRegistry(
                    _containerRegistryUrl,
                    _containerUsername,
                    _containerToken,
                    status => ShowWorkInProgress("Logging into container registry.", status));

            if (!isDockerLoginSuccess)
            {
                throw new Exception("Docker authorization failed (see logs).");
            }

            bool isPushSuccess = await EdgegapBuildUtils.RunCommand_DockerPush(
                _containerRegistryUrl,
                imageRepo,
                tag,
                status => ShowWorkInProgress("Pushing Docker Image", status));

            if (!isPushSuccess)
            {
                throw new Exception("Unable to push docker image to registry (see logs).");
            }
        }

        private void OnBuildContainerizeUploadSuccess(Label displayLabel, string txt)
        {
            EditorUtility.ClearProgressBar();

            displayLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                txt, EdgegapWindowMetadata.StatusColors.Success);
            displayLabel.visible = true;
        }

        private void OnBuildContainerizeUploadError(string dialogMsg, Label displayLabel = null, string labelTxt = null)
        {
            EditorUtility.ClearProgressBar();

            if (displayLabel is not null && !string.IsNullOrEmpty(labelTxt))
            {
                displayLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                labelTxt, EdgegapWindowMetadata.StatusColors.Error);
                displayLabel.visible = true;
            }

            if (!string.IsNullOrEmpty(dialogMsg))
            {
                EditorUtility.DisplayDialog("Error", dialogMsg, "Ok");
            }
        }


#region Persistence Helpers
        /// <summary>
        /// Load from EditorPrefs, persisting from a previous session, if the field is empty
        /// - ApiToken; !persisted via ViewDataKey so we don't save plaintext
        /// - DeploymentRequestId
        /// </summary>
        private void loadPersistentDataFromEditorPrefs()
        {
            // ApiToken
            if (string.IsNullOrEmpty(_apiTokenInput.value))
                setMaskedApiTokenFromEditorPrefs();

            // DeploymentRequestId
            if (string.IsNullOrEmpty(_deploymentRequestId))
                _deploymentRequestId = EditorPrefs.GetString(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR, null);
        }

        /// <summary>Set to base64 -> Save to EditorPrefs</summary>
        /// <param name="value"></param>
        private void persistUnmaskedApiToken(string value)
        {
            EditorPrefs.SetString(
                EdgegapWindowMetadata.API_TOKEN_KEY_STR,
                Base64Encode(value));
        }

        /// <summary>
        /// Get apiToken from EditorPrefs -> Base64 Decode -> Set to apiTokenInput
        /// </summary>
        private void setMaskedApiTokenFromEditorPrefs()
        {
            string apiTokenBase64Str = EditorPrefs.GetString(
                EdgegapWindowMetadata.API_TOKEN_KEY_STR, null);

            if (apiTokenBase64Str == null)
                return;

            string decodedApiToken = Base64Decode(apiTokenBase64Str);
            _apiTokenInput.SetValueWithoutNotify(decodedApiToken);
        }

        /// <summary>
        /// Remove EditorPrefs Keys set by the plugin
        /// </summary>
        private void DeletePersistentDataFromEditorPrefs()
        {
            EditorPrefs.DeleteKey(EdgegapWindowMetadata.API_TOKEN_KEY_STR);
            EditorPrefs.DeleteKey(EdgegapWindowMetadata.DEPLOYMENT_REQUEST_ID_KEY_STR);
        }

        /// <summary>
        /// Remove values stored in form inputs
        /// </summary>
        private void ResetPluginValues()
        {
            _apiTokenInput.value = "";
            _buildFolderNameInput.value = "";
            _buildPathInput.value = "";
            _containerizeImageNameInput.value = "";
            _containerizeImageTagInput.value = "";
            _dockerfilePathInput.value = "";
            _optionalDockerParamsInput.value = "";
            _createAppNameInput.value = "";
            _serverImageNameInput.value = "";
            _serverImageTagInput.value = "";
            _deployAppNameInput.value = "";
            _deployAppVersionInput.value = "";

            _deploymentRequestId = "";
        }
#endregion // Persistence Helpers
    }
}
#endif
