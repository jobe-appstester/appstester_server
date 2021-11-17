using AppsTester.Controller.Submissions;
using Microsoft.EntityFrameworkCore;

namespace AppsTester.Controller
{
    public sealed class ApplicationDbContext : DbContext
    {
        public DbSet<SubmissionCheck> SubmissionChecks { get; set; }

        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
            Database.EnsureCreated();
        }
    }
}