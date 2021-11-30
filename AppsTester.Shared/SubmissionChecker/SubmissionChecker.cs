using System.Threading;
using System.Threading.Tasks;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionChecker
    {
        Task CheckSubmissionAsync(CancellationToken cancellationToken);
    }

    public abstract class SubmissionChecker : ISubmissionChecker
    {
        private readonly ISubmissionResultSetter _submissionResultSetter;

        protected SubmissionChecker(ISubmissionResultSetter submissionResultSetter)
        {
            _submissionResultSetter = submissionResultSetter;
        }

        public async Task CheckSubmissionAsync(CancellationToken cancellationToken)
        {
            var result = await CheckSubmissionCoreAsync(cancellationToken);

            await _submissionResultSetter.SetResultAsync(result, cancellationToken);
        }

        protected abstract Task<object> CheckSubmissionCoreAsync(CancellationToken cancellationToken);
    }
}