namespace AppsTester.Checker.Android.Gradle
{
    internal record GradleTaskExecutionResult(string StandardOutput, string StandardError, int ExitCode)
    {
        public bool IsSuccessful => ExitCode == 0;
    }
}