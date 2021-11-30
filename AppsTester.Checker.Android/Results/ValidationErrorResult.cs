using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record ValidationErrorResult
    (
        [JsonProperty("GradleError")]
        string GradleError
    );
}