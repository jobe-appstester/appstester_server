using System;
using EasyNetQ;
using Newtonsoft.Json;

namespace AppsTester.Shared.Events
{
    [Queue(queueName: "Submissions.ChecksResultEvents")]
    public class SubmissionCheckResultEvent : SubmissionCheckEvent
    {
        public string SerializedResult { get; set; }

        public SubmissionCheckResultEvent WithResult(object result)
        {
            SerializedResult = JsonConvert.SerializeObject(result);

            return this;
        }

        public TResult GetResult<TResult>()
        {
            return JsonConvert.DeserializeObject<TResult>(SerializedResult);
        }
    }
}