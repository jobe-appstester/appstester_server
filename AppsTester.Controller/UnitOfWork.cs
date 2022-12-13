using AppsTester.Controller.Submissions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppsTester.Controller
{
    public interface IUnitOfWork : IDisposable
    {
        ISubmissionsRepository Submissions { get; }
        Task<int> CompleteAsync(CancellationToken cancellationToken);
    }
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;

        public ISubmissionsRepository Submissions { get; }

        public UnitOfWork(ApplicationDbContext dbContext)
        {
            Submissions = new DbContextSubmissionsRepository(dbContext);
            _dbContext = dbContext;
        }

        public Task<int> CompleteAsync(CancellationToken cancellationToken)
            => _dbContext.SaveChangesAsync(cancellationToken);

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}
