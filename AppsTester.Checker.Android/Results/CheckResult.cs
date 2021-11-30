using System.Collections.Generic;

namespace AppsTester.Checker.Android.Results
{
    internal record CheckResult
    (
        int Grade,
        int TotalGrade,
        CheckResultCode ResultCode,
        List<CheckTestResult> TestResults
    );
}