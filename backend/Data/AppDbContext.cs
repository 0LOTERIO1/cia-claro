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
    }
}
