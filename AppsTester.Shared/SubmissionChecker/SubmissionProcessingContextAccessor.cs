using System;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionProcessingContextAccessor
    {
        public SubmissionProcessingContext ProcessingContext { get; }
    }

    public class SubmissionProcessingContextAccessor : ISubmissionProcessingContextAccessor
    {
        public SubmissionProcessingContext ProcessingContext { get; private set; }

        public void SetProcessingContext(SubmissionProcessingContext processingContext)
        {
            if (ProcessingContext != null)
                throw new InvalidOperationException("Can't rewrite already set processing context.");

            ProcessingContext = processingContext;
        }
    }
}