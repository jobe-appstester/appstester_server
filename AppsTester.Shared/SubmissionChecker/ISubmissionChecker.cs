using System.Threading;
using System.Threading.Tasks;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionChecker
    {
        Task CheckSubmissionAsync(CancellationToken cancellationToken);
    }
}