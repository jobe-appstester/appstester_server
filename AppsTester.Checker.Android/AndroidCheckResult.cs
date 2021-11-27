using System.Collections.Generic;
using AppsTester.Shared;

namespace AppsTester.Checker.Android
{
    public class AndroidCheckResult
    {
        public int Grade { get; set; }
        public int TotalGrade { get; set; }
        public string GradleError { get; set; }
        public SubmissionCheckResultCode ResultCode { get; set; }
        public List<SubmissionCheckTestResult> TestResults { get; set; }
    }
}