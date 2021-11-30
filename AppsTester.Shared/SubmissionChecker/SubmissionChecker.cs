using System.Threading.Tasks;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionChecker
    {
        Task CheckSubmissionAsync(SubmissionProcessingContext processingContext);
    }

    public abstract class SubmissionChecker : ISubmissionChecker
    {
        private readonly ISubmissionResultSetter _submissionResultSetter;

        protected SubmissionChecker(ISubmissionResultSetter submissionResultSetter)
        {
            _submissionResultSetter = submissionResultSetter;
        }

        public async Task CheckSubmissionAsync(SubmissionProcessingContext processingContext)
        {
            var result = await CheckSubmissionCoreAsync(processingContext);

            await _submissionResultSetter.SetResultAsync(result);
        }

        protected abstract Task<object> CheckSubmissionCoreAsync(SubmissionProcessingContext processingContext);
    }
}