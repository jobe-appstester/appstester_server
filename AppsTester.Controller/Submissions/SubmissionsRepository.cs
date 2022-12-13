using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppsTester.Controller.Submissions
{
    public interface ISubmissionsRepository
    {
        Task<SubmissionCheck> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);
    }

    public class DbContextSubmissionsRepository : ISubmissionsRepository
    {
        private readonly ApplicationDbContext dbContext;

        public DbContextSubmissionsRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public Task<SubmissionCheck> FindSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
            => dbContext.SubmissionChecks.FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
    }
}
