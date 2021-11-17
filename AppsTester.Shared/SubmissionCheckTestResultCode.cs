namespace AppsTester.Shared
{
    public enum SubmissionCheckTestResultCode
    {
        TestRunning = 1,
        TestPassed = 0,
        AssertionFailure = -2,
        OtherException = -1
    }
}