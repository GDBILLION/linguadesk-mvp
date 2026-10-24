using Microsoft.EntityFrameworkCore;

namespace LinguaDesk.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>().HasKey(x => x.Id);
        modelBuilder.Entity<Customer>().HasIndex(x => new { x.WorkspaceId, x.Email });
        modelBuilder.Entity<Conversation>().HasIndex(x => new { x.WorkspaceId, x.Status, x.UpdatedAt });
        modelBuilder.Entity<Message>().HasIndex(x => new { x.ConversationId, x.CreatedAt });
        modelBuilder.Entity<UsageEvent>().HasIndex(x => new { x.WorkspaceId, x.EventKey }).IsUnique();
        modelBuilder.Entity<Conversation>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>()
            .HasOne(x => x.Conversation).WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
