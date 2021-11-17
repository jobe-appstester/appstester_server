using System;
using System.Collections.Generic;

namespace AppsTester.Shared
{
    public class SubmissionCheckResult
    {
        public Guid Id { get; set; }
        public int Grade { get; set; }
        public int TotalGrade { get; set; }
        public string GradleError { get; set; }
        public SubmissionCheckResultCode ResultCode { get; set; }
        public List<SubmissionCheckTestResult> TestResults { get; set; }
    }
}