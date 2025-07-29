using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    public class UserLocation
    {
        [JsonProperty("user_type")]
        public string UserType { get; set; }

        [JsonProperty("user_data")]
        public UserLocationData UserData { get; set; }
    }
}