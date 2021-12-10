using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionFilesProvider
    {
        Task<Stream> GetFileAsync(string filename);
    }
    
    internal class SubmissionFilesProvider : SubmissionProcessor, ISubmissionFilesProvider
    {
        private readonly IOptions<ControllerOptions> _controllerOptions;
        private readonly IHttpClientFactory _httpClientFactory;

        public SubmissionFilesProvider(IOptions<ControllerOptions> controllerOptions, IHttpClientFactory httpClientFactory)
        {
            _controllerOptions = controllerOptions;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Stream> GetFileAsync(string filename)
        {
            if (!SubmissionCheckRequestEvent.Files.ContainsKey(filename))
                throw new ArgumentException($"Can't find file with name \"{filename}\"");
            
            var httpClient = _httpClientFactory.CreateClient();

            var fileHash = SubmissionCheckRequestEvent.Files[filename];
            return await httpClient.GetStreamAsync(
                requestUri: $"{_controllerOptions.Value.Url}/api/v1/files/{fileHash}",
                cancellationToken: SubmissionProcessingContext.CancellationToken);
        }
    }
}