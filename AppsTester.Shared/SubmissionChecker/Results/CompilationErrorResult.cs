using System.Text;
using Newtonsoft.Json;

namespace AppsTester.Shared.SubmissionChecker.Results
{
    public record CompilationErrorResult
    (
        [JsonProperty("CompilationError")]
        string CompilationError
    );
}