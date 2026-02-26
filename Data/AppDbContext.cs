/*in react badgecraft today i have covered generate the pdf for badges using csv
        * mapped data,added the congiguration layout,format aslso with intigrate  Apis and testing it*/

using BadgeCraft_Net.Models;
using Microsoft.EntityFrameworkCore;

namespace BadgeCraft_Net.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Organization> Organizations => Set<Organization>();

        public DbSet<BadgeTemplate> BadgeTemplates { get; set; }

        public DbSet<Badge> Badges { get; set; }

        public DbSet<UploadJob> UploadJobs { get; set; }
        public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }
       

    }

}
