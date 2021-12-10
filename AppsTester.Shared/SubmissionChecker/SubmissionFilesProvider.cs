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

    internal class SubmissionFilesProvider : ISubmissionFilesProvider
    {
        private readonly IOptions<ControllerOptions> _controllerOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISubmissionProcessingContextAccessor _processingContextAccessor;

        public SubmissionFilesProvider(
            IOptions<ControllerOptions> controllerOptions,
            IHttpClientFactory httpClientFactory,
            ISubmissionProcessingContextAccessor processingContextAccessor)
        {
            _controllerOptions = controllerOptions;
            _httpClientFactory = httpClientFactory;
            _processingContextAccessor = processingContextAccessor;
        }

        public async Task<Stream> GetFileAsync(string filename)
        {
            if (!_processingContextAccessor.ProcessingContext.Event.Files.ContainsKey(filename))
                throw new ArgumentException($"Can't find file with name \"{filename}\"");

            var httpClient = _httpClientFactory.CreateClient();

            var fileHash = _processingContextAccessor.ProcessingContext.Event.Files[filename];
            return await httpClient.GetStreamAsync(
                requestUri: $"{_controllerOptions.Value.Url}/api/v1/files/{fileHash}",
                cancellationToken: _processingContextAccessor.ProcessingContext.CancellationToken);
        }
    }
}