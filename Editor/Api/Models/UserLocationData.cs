using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    public class UserLocationData
    {
        [JsonProperty("ip_address")]
        public string IpAddress { get; set; }

        [JsonProperty("latitude")]
        public double Latitude { get; set; }
            
        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }
}