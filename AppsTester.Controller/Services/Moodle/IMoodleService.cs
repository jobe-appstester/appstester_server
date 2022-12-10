using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Controller.Submissions;

namespace AppsTester.Controller.Services.Moodle
{
    public interface IMoodleService
    {
        Task<List<SubmissionsUnit>> GetSubmissionsToCheckAsync(CancellationToken stoppingToken);
        Task<Submission> GetSubmissionAsync(int id, CancellationToken stoppingToken, string includedFileHashes = "");
        Task SetSubmissionStatusAsync(int id, string status, CancellationToken stoppingToken);
        Task SetSubmissionResultAsync(int id, string result, CancellationToken stoppingToken);
    }
}