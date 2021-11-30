using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Statuses
{
    internal record ProcessingStatus
    (
        [JsonProperty("Status")]
        string Status
    );
}