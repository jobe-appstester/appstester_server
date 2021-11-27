using System;
using Newtonsoft.Json;

namespace AppsTester.Shared.Events
{
    public class SubmissionCheckResultEvent : SubmissionCheckEvent
    {
        public string SerializedResult { get; set; }

        public SubmissionCheckResultEvent(SubmissionCheckRequestEvent requestEvent, object result)
            : this(submissionId: requestEvent.SubmissionId, result)
        {
        }

        public SubmissionCheckResultEvent(Guid submissionId, object result) : base(submissionId)
        {
            SetResult(result);
        }

        public void SetResult(object result)
        {
            SerializedResult = JsonConvert.SerializeObject(result);
        }

        public TResult GetResult<TResult>()
        {
            return JsonConvert.DeserializeObject<TResult>(SerializedResult);
        }
    }
}