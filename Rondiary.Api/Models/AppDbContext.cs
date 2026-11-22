using Microsoft.EntityFrameworkCore;

namespace Rondiary.Api.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Mood> Moods => Set<Mood>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Entry> Entries => Set<Entry>();


    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Category>().HasIndex(c => new { c.UserId, c.Name }).IsUnique();
        b.Entity<Entry>().HasIndex(e => new { e.UserId, e.CreatedAt });
        b.Entity<Entry>().HasOne(e => e.Parent).WithMany().HasForeignKey(e => e.ParentEntryId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Entry>().HasMany(e => e.Categories).WithMany(c => c.Entries).UsingEntity(j => j.ToTable("EntryCategories"));

        b.Entity<Mood>().HasData(
    new Mood { Id = 1, Name = "Радость", IconKey = "joy" },
    new Mood { Id = 2, Name = "Спокойствие", IconKey = "calm" },
    new Mood { Id = 3, Name = "Грусть", IconKey = "sad" },
    new Mood { Id = 4, Name = "Злость", IconKey = "angry" },
    new Mood { Id = 5, Name = "Усталость", IconKey = "tired" },
    new Mood { Id = 6, Name = "Вдохновение", IconKey = "inspired" },
    new Mood { Id = 7, Name = "Сердечко", IconKey = "love" });
    }
}