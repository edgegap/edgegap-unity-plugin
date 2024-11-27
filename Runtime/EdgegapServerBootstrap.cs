using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Edgegap.Bootstrap
{
    public class EdgegapServerBootstrap : MonoBehaviour
    {
        public static EdgegapServerBootstrap Instance { get; protected set; }
        public static string BootstrapObjectName = "EdgegapServerBootstrap";

        protected const string APP_VERSION_PAGE = "https://app.edgegap.com/application-management/applications/list";

        protected ArbitriumPortsMapping _arbitriumPortsMapping;
        protected ArbitriumDeploymentLocation _arbitriumDeploymentLocation;
        protected Dictionary<string, string> _arbitriumSimpleEnvs = new Dictionary<string, string>();

        protected enum EdgegapTransportProtocols
        {
            TCP,
            UDP,
            HTTP,
            HTTPS,
            TCP_UDP,
            WS,
            WSS
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

        private void Start()
        {
#if !UNITY_CLIENT && !UNITY_EDITOR
            ParseEdgegapEnvs();
            ValidatePortMapping();
#endif
        }

        private void ParseEdgegapEnvs()
        {
            IDictionary envs = Environment.GetEnvironmentVariables();

            foreach (DictionaryEntry envEntry in envs)
            {
                if (envEntry.Key.ToString().Contains("ARBITRIUM_"))
                {
                    if (envs.Contains("ARBITRIUM_ENV_DEBUG"))
                    {
                        Debug.Log($"{envEntry.Key}: {envEntry.Value}");
                    }

                    if (envEntry.Key.ToString().Contains("DEPLOYMENT_LOCATION"))
                    {
                        _arbitriumDeploymentLocation = JsonConvert.DeserializeObject<ArbitriumDeploymentLocation>(envEntry.Value.ToString());
                    }
                    else if (envEntry.Key.ToString().Contains("PORTS_MAPPING"))
                    {
                        _arbitriumPortsMapping = JsonConvert.DeserializeObject<ArbitriumPortsMapping>(envEntry.Value.ToString());
                    }
                    else
                    {
                        _arbitriumSimpleEnvs[envEntry.Key.ToString()] = envEntry.Value.ToString();
                    }
                }
            }
        }

        protected virtual void ValidatePortMapping()
        {
            throw new NotImplementedException();
        }
    }
}
