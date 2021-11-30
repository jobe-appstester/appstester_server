using System;

namespace AppsTester.Shared.SubmissionChecker.Events
{
    public abstract class SubmissionCheckEvent
    {
        public Guid SubmissionId { get; set; }
    }
}