using System;

namespace AppsTester.Shared.Events
{
    public abstract class SubmissionCheckEvent
    {
        public Guid SubmissionId { get; }

        public DateTime OccurenceDateTime { get; }

        protected SubmissionCheckEvent(Guid submissionId, DateTime? occurenceDateTime = null)
        {
            SubmissionId = submissionId;
            OccurenceDateTime = occurenceDateTime ?? DateTime.UtcNow;
        }
    }
}