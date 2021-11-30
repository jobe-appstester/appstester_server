using System.Collections.Generic;
using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record CheckResult
    (
        [JsonProperty("Gradle")]
        int Grade,
        [JsonProperty("TotalGrade")]
        int TotalGrade,
        [JsonProperty("ResultCode")]
        CheckResultCode ResultCode,
        [JsonProperty("TestResults")]
        List<CheckTestResult> TestResults
    );
}