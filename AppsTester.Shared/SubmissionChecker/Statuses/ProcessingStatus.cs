using Newtonsoft.Json;

namespace AppsTester.Shared.SubmissionChecker.Statuses
{
    public record ProcessingStatus
    (
        [JsonProperty("Status")]
        string Status
    );
}