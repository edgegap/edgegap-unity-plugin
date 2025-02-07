using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Edgegap.Bootstrap
{
    public abstract class EdgegapServerBootstrap : MonoBehaviour
    {
        public static EdgegapServerBootstrap Instance { get; protected set; }
        public static string BootstrapObjectName = "EdgegapServerBootstrap";

        protected const string APP_VERSION_PAGE =
            "https://app.edgegap.com/application-management/applications/list";

        protected ArbitriumPortsMapping _arbitriumPortsMapping;
        protected ArbitriumDeploymentLocation _arbitriumDeploymentLocation;
        protected Dictionary<string, string> _arbitriumSimpleEnvs =
            new Dictionary<string, string>();

        protected enum EdgegapTransportProtocols
        {
            TCP,
            UDP,
            HTTP,
            HTTPS,
            TCP_UDP,
            WS,
            WSS,
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }
        }

#if !UNITY_CLIENT && !UNITY_EDITOR
        private void Start()
        {
            ParseEdgegapEnvs();
            ValidatePortMapping();
        }
#endif

        private void ParseEdgegapEnvs()
        {
            IDictionary envs = Environment.GetEnvironmentVariables();

            foreach (DictionaryEntry envEntry in envs)
            {
                if (!envEntry.Key.ToString().Contains("ARBITRIUM_"))
                {
                    continue;
                }

                string envKey = envEntry.Key.ToString();
                string envValue = envEntry.Value.ToString();

                if (envs.Contains("ARBITRIUM_ENV_DEBUG"))
                {
                    Debug.Log($"{envKey}: {envValue}");
                }

                if (envKey.Contains("DEPLOYMENT_LOCATION"))
                {
                    _arbitriumDeploymentLocation =
                        JsonConvert.DeserializeObject<ArbitriumDeploymentLocation>(envValue);
                }
                else if (envKey.Contains("PORTS_MAPPING"))
                {
                    _arbitriumPortsMapping = JsonConvert.DeserializeObject<ArbitriumPortsMapping>(
                        envValue
                    );
                }
                else
                {
                    _arbitriumSimpleEnvs[envKey] = envValue;
                }
            }
        }

        protected abstract void ValidatePortMapping();
    }
}
