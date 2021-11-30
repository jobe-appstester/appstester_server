using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record CompilationErrorResult
    (
        [JsonProperty("GradleError")]
        string GradleError
    );
}