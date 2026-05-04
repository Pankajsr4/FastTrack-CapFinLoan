using CapFinLoan.Document.Domain.Constants;
using CapFinLoan.Document.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Document.Persistence.Data;

public class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }

    public DbSet<LoanDocument> Documents => Set<LoanDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LoanDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.FileName).IsRequired().HasMaxLength(256);
            entity.Property(d => d.StoredFileName).IsRequired().HasMaxLength(512);
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(d => d.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(d => d.Remarks).HasMaxLength(1000);
            entity.Property(d => d.FailureReason).HasMaxLength(2000);

            // Store Status as a readable string (e.g. "Pending", "Verified", "ReuploadRequired")
            entity.Property(d => d.Status)
                  .HasConversion<string>()
                  .HasMaxLength(30)
                  .HasDefaultValue(DocumentStatus.Pending)
                  .IsRequired();

            // IsVerified is a computed property — ignore it in EF mapping
            entity.Ignore(d => d.IsVerified);

            entity.HasIndex(d => d.ApplicationId);
            entity.HasIndex(d => d.UserId);
        });
    }
}
