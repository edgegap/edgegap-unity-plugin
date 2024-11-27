using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Edgegap.Bootstrap
{
    public class ArbitriumPortsMapping
    {
        [JsonProperty("ports")]
        public Dictionary<string, PortMappingData> ports { get; private set; }
    }
}
