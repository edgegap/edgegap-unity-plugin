using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Edgegap.Editor.Api
{
    public class AnalyticsApi
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private string _url = "https://r.edgegap.net/";
        private string _Key = "phc_sjDOXB5OakYZu0h70u4GLcFR7hZ55XfnnDef5xaeDws";
        private string _Event = "Plugin Button Click";

        private class AnalyticsPayload
        {
            [JsonProperty("api_key")]
            public string ApiKey { get; set; }

            [JsonProperty("event")]
            public string Event { get; set; }

            [JsonProperty("distinct_id")]
            public string DistinctId { get; set; }

            [JsonProperty("properties")]
            public Dictionary<string, string> Properties { get; set; }

            public AnalyticsPayload(
                string apiKey,
                string ev,
                string disctinctId,
                Dictionary<string, string> properties
            )
            {
                this.ApiKey = apiKey;
                this.Event = ev;
                this.DistinctId = disctinctId;
                this.Properties = properties;
            }

            public override string ToString() => JsonConvert.SerializeObject(this);
        }

        public AnalyticsApi()
        {
            this._httpClient.BaseAddress = new Uri(_url);
            this._httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public async Task<HttpResponseMessage> PostAsync(
            string distinctId,
            Dictionary<string, string> properties
        )
        {
            AnalyticsPayload payload = new AnalyticsPayload(_Key, _Event, distinctId, properties);
            StringContent stringContent = new StringContent(
                payload.ToString(),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                return await _httpClient.PostAsync(_httpClient.BaseAddress, stringContent);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }
    }
}
