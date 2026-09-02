using Cia.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ConversationContext> ConversationContexts => Set<ConversationContext>();
    public DbSet<Handoff> Handoffs => Set<Handoff>();
    public DbSet<DepartmentTransfer> DepartmentTransfers => Set<DepartmentTransfer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<HumanAgentRequest> HumanAgentRequests => Set<HumanAgentRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(40);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
            entity.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<ConversationSession>(entity =>
        {
            entity.ToTable("conversation_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Protocol).IsRequired().HasMaxLength(32);
            entity.HasIndex(x => x.Protocol).IsUnique();
            entity.Property(x => x.CustomerId).IsRequired().HasMaxLength(40);
            entity.Property(x => x.InitialChannel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CurrentChannel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CurrentDepartment).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.PreviousDepartment).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.DetectedIntent).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.CustomerId, x.Status });

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sender).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Content).IsRequired().HasMaxLength(4000);
            entity.HasIndex(x => x.SessionId);

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationContext>(entity =>
        {
            entity.ToTable("conversation_contexts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SessionId).IsUnique();
            entity.Property(x => x.IssueType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.OriginalProblem).HasMaxLength(500);
            entity.Property(x => x.TroubleshootingPerformed).HasMaxLength(1000);
            entity.Property(x => x.CurrentRequest).HasMaxLength(500);
            entity.Property(x => x.ImportantFacts).HasMaxLength(4000);
            entity.Property(x => x.ContextSummary).HasMaxLength(2000);
            entity.Property(x => x.AdditionalData).HasMaxLength(4000);

            entity.HasOne(x => x.Session)
                .WithOne(x => x.Context)
                .HasForeignKey<ConversationContext>(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Handoff>(entity =>
        {
            entity.ToTable("handoffs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Summary).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.SessionId);

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Handoffs)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DepartmentTransfer>(entity =>
        {
            entity.ToTable("department_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromDepartment).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ToDepartment).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(300);
            entity.HasIndex(x => x.SessionId);

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Transfers)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(180);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CustomerId).HasMaxLength(40);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HumanAgentRequest>(entity =>
        {
            entity.ToTable("human_agent_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.SessionId);

            entity.HasOne(x => x.Session)
                .WithMany(x => x.HumanAgentRequests)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AssignedAgent)
                .WithMany(x => x.AssignedRequests)
                .HasForeignKey(x => x.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
