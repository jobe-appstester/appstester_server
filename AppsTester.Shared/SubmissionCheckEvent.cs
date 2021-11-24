using System;
using EasyNetQ;

namespace AppsTester.Shared
{
    [Queue(queueName: "Submissions.CheckEvents")]
    public class SubmissionCheckEvent
    {
        public Guid SubmissionId { get; set; }
        public DateTime OccurenceDateTime { get; set; }
    }
}