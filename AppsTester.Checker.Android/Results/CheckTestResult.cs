namespace AppsTester.Checker.Android.Results
{
    internal record CheckTestResult
    (
        string Test,
        string Class,
        string Stream,
        CheckTestResultCode ResultCode
    );
}