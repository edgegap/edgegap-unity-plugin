using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `POST v2/deployments`.
    /// </summary>
    public class CreateDeploymentResult
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }
    }
}
