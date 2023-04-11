using Newtonsoft.Json;

namespace AppsTester.Shared.SubmissionChecker.Results
{
    public record ValidationErrorResult
    (
        [JsonProperty("ValidationError")]
        string ValidationError
    );
}