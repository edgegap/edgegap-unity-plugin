using System.Collections.Generic;
using Newtonsoft.Json;

namespace Edgegap.Bootstrap
{
    public class ArbitriumPortsMapping
    {
        [JsonProperty("ports")]
        public Dictionary<string, PortMappingData> ports { get; private set; }
    }
}
