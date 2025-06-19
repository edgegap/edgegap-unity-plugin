using Newtonsoft.Json;

namespace Edgegap.Bootstrap
{
    /// <summary>
    /// Detailed information about the deployment's location
    /// </summary>
    public class ArbitriumDeploymentLocation
    {
        [JsonProperty("city")]
        public string city { get; private set; }

        [JsonProperty("country")]
        public string country { get; private set; }

        [JsonProperty("continent")]
        public string continent { get; private set; }

        [JsonProperty("administrative_division")]
        public string administrativeDivision { get; private set; }

        [JsonProperty("timezone")]
        public string timezone { get; private set; }

        [JsonProperty("latitude")]
        public float latitude { get; private set; }

        [JsonProperty("longitude")]
        public string longitude { get; private set; }
    }
}
