namespace AppsTester.Checker.Android.Gradle
{
    public record GradleTaskExecutionResult(string StandardOutput, string StandardError, int ExitCode);
}