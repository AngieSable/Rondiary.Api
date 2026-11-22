namespace Rondiary.Api.Models
{
    public enum EntryType { Text, Video, Photo, Audio, File }

    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Mood
    {
        public short Id { get; set; }
        public string Name { get; set; } = "";
        public string IconKey { get; set; } = "";
    }

    public class Category
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string Name { get; set; } = "";
        public List<Entry> Entries { get; set; } = [];
    }

    public class Entry
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public Guid? ParentEntryId { get; set; }
        public Entry? Parent { get; set; }
        // Ответ на сообщение (просто ссылка, без внешнего ключа)
        public Guid? ReplyToEntryId { get; set; }
        // Альбом: у фото, отправленных вместе, общий номер
        public Guid? AlbumId { get; set; }
        public EntryType Type { get; set; }
        public string? MediaPath { get; set; }
        public string? Content { get; set; }
        public short? MoodId { get; set; }
        public Mood? Mood { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; }
        public List<Category> Categories { get; set; } = [];
    }
}