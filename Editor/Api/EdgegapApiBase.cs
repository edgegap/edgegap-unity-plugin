using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Handles base URL and common methods for all Edgegap APIs.
    /// </summary>
    public abstract class EdgegapApiBase
    {
        #region Vars
        private readonly HttpClient _httpClient = new HttpClient(); // Base address set // MIRROR CHANGE: Unity 2020 support
        protected ApiEnvironment SelectedApiEnvironment { get; }
        protected EdgegapWindowMetadata.LogLevel LogLevel { get; set; }
        protected bool IsLogLevelDebug => LogLevel == EdgegapWindowMetadata.LogLevel.Debug;

        /// <summary>Based on SelectedApiEnvironment.</summary>
        /// <returns></returns>
        private string GetBaseUrl() =>
            SelectedApiEnvironment == ApiEnvironment.Staging
                ? ApiEnvironment.Staging.GetApiUrl()
                : ApiEnvironment.Console.GetApiUrl();
        #endregion // Vars


        /// <param name="apiEnvironment">"console" || "staging-console"?</param>
        /// <param name="apiToken">Without the "token " prefix, although we'll clear this if present</param>
        /// <param name="logLevel">You may want more-verbose logs other than errs</param>
        protected EdgegapApiBase(
            ApiEnvironment apiEnvironment,
            string apiToken,
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error
        )
        {
            this.SelectedApiEnvironment = apiEnvironment;

            this._httpClient.BaseAddress = new Uri($"{GetBaseUrl()}/");
            this._httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            string cleanedApiToken = apiToken.Replace("token ", ""); // We already prefixed token below
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "token",
                cleanedApiToken
            );

            this.LogLevel = logLevel;
        }

        #region HTTP Requests
        /// <summary>
        /// POST | We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="json">Serialize to your model via Newtonsoft</param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> PostAsync(
            string relativePath = "",
            string json = "{}"
        )
        {
            StringContent stringContent = CreateStringContent(json);
            Uri uri = new Uri(_httpClient.BaseAddress, relativePath); // Normalize POST uri: Can't end with `/`.

            if (IsLogLevelDebug)
                Debug.Log($"PostAsync to: `{uri}` with json: `{json}`");

            try
            {
                return await ExecuteRequestAsync(() => _httpClient.PostAsync(uri, stringContent));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// <summary>
        /// PATCH | We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="json">Serialize to your model via Newtonsoft</param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> PatchAsync(
            string relativePath = "",
            string json = "{}"
        )
        {
            StringContent stringContent = CreateStringContent(json);
            Uri uri = new Uri(_httpClient.BaseAddress, relativePath); // Normalize PATCH uri: Can't end with `/`.

            if (IsLogLevelDebug)
                Debug.Log($"PatchAsync to: `{uri}` with json: `{json}`");

            // (!) As of 11/15/2023, .PatchAsync() is "unsupported by Unity" -- so we manually set the verb and SendAsync()
            // Create the request manually
            HttpRequestMessage patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), uri)
            {
                Content = stringContent,
            };

            try
            {
                return await ExecuteRequestAsync(() => _httpClient.SendAsync(patchRequest));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// <summary>
        /// GET | We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> GetAsync(string relativePath = "")
        {
            Uri uri = new Uri(_httpClient.BaseAddress, relativePath);

            if (IsLogLevelDebug)
                Debug.Log($"GetAsync to: `{uri}`");

            try
            {
                return await ExecuteRequestAsync(() => _httpClient.GetAsync(uri));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// <summary>
        /// DELETE | We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> DeleteAsync(string relativePath = "")
        {
            Uri uri = new Uri(_httpClient.BaseAddress, relativePath);

            if (IsLogLevelDebug)
                Debug.Log($"DeleteAsync to: `{uri}`");

            try
            {
                return await ExecuteRequestAsync(() => _httpClient.DeleteAsync(uri));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                throw;
            }
        }

        /// <summary>POST || GET</summary>
        /// <param name="requestFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task<HttpResponseMessage> ExecuteRequestAsync(
            Func<Task<HttpResponseMessage>> requestFunc,
            CancellationToken cancellationToken = default
        )
        {
            HttpResponseMessage response = null;
            try
            {
                response = await requestFunc();
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"HttpRequestException: {e.Message}");
                return null;
            }
            catch (TaskCanceledException e)
            {
                if (cancellationToken.IsCancellationRequested)
                    Debug.LogError("Task was cancelled by caller.");
                else
                    Debug.LogError($"TaskCanceledException: Timeout - {e.Message}");
                return null;
            }
            catch (Exception e) // Generic exception handler
            {
                Debug.LogError($"Unexpected error occurred: {e.Message}");
                return null;
            }

            // Check for a successful status code
            if (response == null)
            {
                Debug.Log("Error: (null response) - returning 500");
                return CreateUnknown500Err();
            }

            if (!response.IsSuccessStatusCode)
            {
                HttpMethod httpMethod = response.RequestMessage.Method;
                Debug.Log(
                    $"Error: {(short)response.StatusCode} {response.ReasonPhrase} - "
                        + $"{httpMethod} | {response.RequestMessage.RequestUri}` - "
                        + $"{response.Content?.ReadAsStringAsync().Result}"
                );
            }

            return response;
        }
        #endregion // HTTP Requests


        #region Utils
        /// <summary>Creates a UTF-8 encoded application/json + json obj</summary>
        /// <param name="json">Arbitrary json obj</param>
        /// <returns></returns>
        private StringContent CreateStringContent(string json = "{}") =>
            new StringContent(json, Encoding.UTF8, "application/json"); // MIRROR CHANGE: 'new()' not supported in Unity 2020

        private static HttpResponseMessage CreateUnknown500Err() =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError); // 500 - Unknown // MIRROR CHANGE: 'new()' not supported in Unity 2020
        #endregion // Utils
    }
}
