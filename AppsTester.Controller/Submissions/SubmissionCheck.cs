using System;

namespace AppsTester.Controller.Submissions
{
    public class SubmissionCheck
    {
        public Guid Id { get; set; }
        
        public int AttemptId { get; set; }

        public string SubmissionCheckStatus { get; set; }
        
        public DateTime SendingDateTimeUtc { get; set; } = DateTime.UtcNow;
    }
}