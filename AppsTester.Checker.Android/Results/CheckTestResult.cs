using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record CheckTestResult
    (
        [JsonProperty("Test")]
        string Test,
        [JsonProperty("Class")]
        string Class,
        [JsonProperty("Stream")]
        string Stream,
        [JsonProperty("ResultCode")]
        CheckTestResultCode ResultCode
    );
}