using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `GET /v1/wizard/organization-information`.
    /// </summary>
    public class GetOrganizationInformationResult
    {
        [JsonProperty("distinct_id")]
        public string DistinctId { get; set; }

        [JsonProperty("identifier")]
        public string Identifier { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }
    }
}
