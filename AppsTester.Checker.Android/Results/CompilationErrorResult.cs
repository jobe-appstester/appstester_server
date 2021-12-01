using System.Text;
using AppsTester.Checker.Android.Gradle;
using Newtonsoft.Json;

namespace AppsTester.Checker.Android.Results
{
    internal record CompilationErrorResult
    (
        [JsonProperty("CompilationError")]
        string CompilationError
    )
    {
        public CompilationErrorResult(GradleTaskExecutionResult taskExecutionResult)
            : this(GetErrorFromTaskResult(taskExecutionResult))
        {
        }

        private static string GetErrorFromTaskResult(GradleTaskExecutionResult taskExecutionResult)
        {
            var totalErrorStringBuilder = new StringBuilder();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardOutput);
            totalErrorStringBuilder.AppendLine();
            totalErrorStringBuilder.AppendLine(taskExecutionResult.StandardError);

            return totalErrorStringBuilder.ToString().Trim();
        }
    };
}