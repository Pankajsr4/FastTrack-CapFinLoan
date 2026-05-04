using CapFinLoan.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Admin.Persistence.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<DocumentProcessingRecord> DocumentProcessingRecords => Set<DocumentProcessingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<LoanApplication>(entity =>
        {
            entity.ToTable("LoanApplications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicationNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.Gender).HasMaxLength(20);
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.AddressLine1).HasMaxLength(250);
            entity.Property(x => x.AddressLine2).HasMaxLength(250);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.State).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(15);
            entity.Property(x => x.EmployerName).HasMaxLength(150);
            entity.Property(x => x.EmploymentType).HasMaxLength(50);
            entity.Property(x => x.MonthlyIncome).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AnnualIncome).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ExistingEmiAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.LoanPurpose).HasMaxLength(200);
            entity.Property(x => x.Remarks).HasMaxLength(1000);
            entity.Property(x => x.DisbursedAmount).HasColumnType("decimal(18,2)");

            entity.HasMany(x => x.StatusHistory)
                .WithOne(x => x.LoanApplication)
                .HasForeignKey(x => x.LoanApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Decisions)
                .WithOne(x => x.LoanApplication)
                .HasForeignKey(x => x.LoanApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.ToTable("ApplicationStatusHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStatus).HasMaxLength(30);
            entity.Property(x => x.ToStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(1000);
        });

        modelBuilder.Entity<Decision>(entity =>
        {
            entity.ToTable("Decisions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DecisionStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Remarks).HasMaxLength(1000);
            entity.Property(x => x.SanctionAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.InterestRate).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<DocumentProcessingRecord>(entity =>
        {
            entity.ToTable("DocumentProcessingRecords");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DocumentId).IsUnique();
            entity.HasIndex(x => x.ApplicationId);
            entity.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FailureReason).HasMaxLength(2000);
        });
    }
}