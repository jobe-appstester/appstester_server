using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AppsTester.Controller.Moodle
{
    internal interface IMoodleCommunicator
    {
        Task<TResult> GetFunctionResultAsync<TResult>(
            string functionName,
            IDictionary<string, object> functionParams = null,
            CancellationToken cancellationToken = default);

        Task CallFunctionAsync(
            string functionName,
            IDictionary<string, object> functionParams = null,
            IDictionary<string, string> requestParams = null,
            CancellationToken cancellationToken = default);
    }

    internal class MoodleCommunicator : IMoodleCommunicator
    {
        private readonly MoodleOptions _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public MoodleCommunicator(IOptions<MoodleOptions> options, IHttpClientFactory httpClientFactory)
        {
            _configuration = options.Value;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<TResult> GetFunctionResultAsync<TResult>(
            string functionName,
            IDictionary<string, object> functionParams = null,
            CancellationToken cancellationToken = default)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var queryParams = new Dictionary<string, string>
            {
                ["moodlewsrestformat"] = "json",
                ["wstoken"] = _configuration.Token,
                ["wsfunction"] = functionName
            };

            if (functionParams != null)
                foreach (var (name, value) in functionParams)
                    queryParams.Add(name, value.ToString());

            var requestUri = $"{_configuration.Url}/webservice/rest/server.php";
            var uriWithParams = QueryHelpers.AddQueryString(requestUri, queryParams);

            var request = new HttpRequestMessage(method: HttpMethod.Get, requestUri: uriWithParams);

            if (_configuration.BasicToken != null)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", _configuration.BasicToken);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            return JsonConvert.DeserializeObject<TResult>(content);
        }

        public async Task CallFunctionAsync(
            string functionName,
            IDictionary<string, object> functionParams = null,
            IDictionary<string, string> requestParams = null,
            CancellationToken cancellationToken = default)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var queryParams = new Dictionary<string, string>
            {
                ["moodlewsrestformat"] = "json",
                ["wstoken"] = _configuration.Token,
                ["wsfunction"] = functionName
            };

            if (functionParams != null)
                foreach (var (name, value) in functionParams)
                    queryParams.Add(name, value.ToString());

            var requestUri = $"{_configuration.Url}/webservice/rest/server.php";
            var uriWithParams = QueryHelpers.AddQueryString(requestUri, queryParams);

            var request = new HttpRequestMessage(method: HttpMethod.Post, requestUri: uriWithParams);

            if (_configuration.BasicToken != null)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", _configuration.BasicToken);
            }

            if (requestParams != null)
                request.Content = new FormUrlEncodedContent(requestParams);

            await httpClient.SendAsync(request, cancellationToken);
        }
    }
}