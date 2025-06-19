using Newtonsoft.Json;
using System.Collections.Generic;

namespace Edgegap.Bootstrap
{
    /// <summary>
    /// Detailed information about the deployment's internal and external ports
    /// </summary>
    public class PortMappingData
    {
        [JsonProperty("name")]
        public string name { get; private set; }

        [JsonProperty("internal")]
        public int internalPort { get; private set; }

        [JsonProperty("external")]
        public int externalPort { get; private set; }

        [JsonProperty("protocol")]
        public string protocol { get; private set; }
    }
}
