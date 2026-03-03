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
        public DbSet<BadgeTemplateField> BadgeTemplateFields { get; set; }
        public DbSet<UploadJob> UploadJobs { get; set; }
        public DbSet<GeneratedDocument> GeneratedDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===============================
            // Organization Relationships
            // ===============================

            modelBuilder.Entity<BadgeTemplate>()
                .HasOne(t => t.Organization)
                .WithMany(o => o.BadgeTemplates)
                .HasForeignKey(t => t.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UploadJob>()
                .HasOne<Organization>()
                .WithMany(o => o.UploadJobs)
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===============================
            // UploadJob Relationships
            // ===============================

            modelBuilder.Entity<UploadJob>()
                .HasOne(u => u.Template)
                .WithMany()
                .HasForeignKey(u => u.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            // One-to-One: UploadJob → GeneratedDocument
            modelBuilder.Entity<GeneratedDocument>()
                .HasOne(g => g.UploadJob)
                .WithOne(u => u.GeneratedDocument)
                .HasForeignKey<GeneratedDocument>(g => g.UploadJobId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===============================
            // Badge Relationships
            // ===============================

            modelBuilder.Entity<Badge>()
                .HasOne(b => b.BadgeTemplate)
                .WithMany()
                .HasForeignKey(b => b.BadgeTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}