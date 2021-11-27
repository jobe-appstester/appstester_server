using System;

namespace AppsTester.Shared.Events
{
    public abstract class SubmissionCheckEvent
    {
        public Guid SubmissionId { get; set; }
    }
}