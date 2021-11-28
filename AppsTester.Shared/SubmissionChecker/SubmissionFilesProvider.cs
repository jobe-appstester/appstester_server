using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionFilesProvider
    {
        Task<MemoryStream> GetFileAsync(string filename);
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

        public async Task<MemoryStream> GetFileAsync(string filename)
        {
            if (!SubmissionCheckRequestEvent.Files.ContainsKey(filename))
                throw new ArgumentException($"Can't find file with name \"{filename}\"");
            
            using var httpClient = _httpClientFactory.CreateClient();

            var fileHash = SubmissionCheckRequestEvent.Files[filename];
            var fileStream = await httpClient.GetStreamAsync($"{_controllerOptions.Value.Url}/api/v1/files/{fileHash}");

            var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);

            return memoryStream;
        }
    }
}