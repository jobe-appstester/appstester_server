using System;
using EasyNetQ;
using Newtonsoft.Json;

namespace AppsTester.Shared.Events
{
    [Queue(queueName: "Submissions.CheckStatusEvents")]
    public class SubmissionCheckStatusEvent : SubmissionCheckEvent
    {
        public string SerializedStatus { get; set; }

        public SubmissionCheckStatusEvent(SubmissionCheckRequestEvent requestEvent, object result)
            : this(submissionId: requestEvent.SubmissionId, result)
        {
        }

        public SubmissionCheckStatusEvent(Guid submissionId, object status) : base(submissionId)
        {
            SetStatus(status);
        }

        public void SetStatus(object status)
        {
            SerializedStatus = JsonConvert.SerializeObject(status);
        }

        public TStatus GetStatus<TStatus>()
        {
            return JsonConvert.DeserializeObject<TStatus>(SerializedStatus);
        }
    }
}