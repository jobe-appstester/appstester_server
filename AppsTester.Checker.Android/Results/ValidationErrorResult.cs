using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record ValidationErrorResult
    (
        [JsonProperty("ValidationError")]
        string ValidationError
    );
}