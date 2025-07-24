using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for `POST v2/deployments`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deploy
    /// </summary>
    public class CreateDeploymentRequest
    {
        #region Required
        /// <summary>*Required: The name of the App you want to deploy.</summary>
        [JsonProperty("application")]
        public string AppName { get; set; }

        /// <summary>
        /// *Required: The name of the App Version you want to deploy;
        /// </summary>
        [JsonProperty("version")]
        public string VersionName { get; set; }

        /// <summary>
        /// *Required: The list of IP/location of your user.
        /// </summary>
        [JsonProperty("users")]
        public UserLocation[] Users { get; set; }
        #endregion // Required

        /// <summary>
        /// The list of tags assigned to the deployment
        /// </summary>
        [JsonProperty("tags")]
        public string[] Tags { get; set; } = { EdgegapWindowMetadata.DEFAULT_DEPLOYMENT_TAG };

        /// <summary>Used by Newtonsoft</summary>
        public CreateDeploymentRequest() { }

        /// <summary>Init with required info; used for a single external IP address.</summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="versionName">
        /// The name of the App Version you want to deploy, if not present,
        /// the last version created is picked.
        /// </param>
        /// <param name="externalIp">Obtain from IpApi.</param>
        public CreateDeploymentRequest(string appName, string versionName, string externalIp)
        {
            this.AppName = appName;
            this.VersionName = versionName;
            
            UserLocation user = new UserLocation()
            {
                UserType = "ip_address",
                UserData = new UserLocationData()
                {
                    IpAddress = externalIp
                }
            };

            this.Users = new[] { user };
        }

        /// <summary>Parse to json str</summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}
