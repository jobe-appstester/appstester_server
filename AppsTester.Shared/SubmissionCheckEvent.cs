using System;

namespace AppsTester.Shared
{
    public class SubmissionCheckEvent
    {
        public Guid SubmissionId { get; set; }
        public DateTime OccurenceDateTime { get; set; }
    }
}