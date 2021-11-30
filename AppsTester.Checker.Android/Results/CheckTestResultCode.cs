namespace AppsTester.Checker.Android.Results
{
    internal enum CheckTestResultCode
    {
        TestRunning = 1,
        TestPassed = 0,
        AssertionFailure = -2,
        OtherException = -1
    }
}