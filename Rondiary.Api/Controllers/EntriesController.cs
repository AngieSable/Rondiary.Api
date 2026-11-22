using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rondiary.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Rondiary.Api.Controllers;

public record EntryDto(Guid Id, EntryType Type, string? MediaUrl, string? Content,
    short? MoodId, Guid? ParentEntryId, DateTime CreatedAt, int RepliesCount, bool IsEdited,
    Guid? ReplyToEntryId, Guid? CategoryId, Guid? AlbumId);

public record CreateEntryRequest(EntryType Type, string? Content, Guid? ParentEntryId, short? MoodId,
    Guid? Id = null, Guid? ReplyToEntryId = null, Guid? CategoryId = null);

public record UpdateEntryRequest(string? Content, short? MoodId, bool IsEdited = false, Guid? CategoryId = null);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EntriesController(AppDbContext db, SupabaseStorage storage) : ControllerBase
{

    private Guid UserId => User.GetUserId();
    private const long MaxVideoBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/heic", "image/heif", "image/bmp"
    };

    [HttpGet]
    public async Task<List<EntryDto>> Get(Guid? parentId, short? moodId, Guid? categoryId, string? search)
    {
        var query = db.Entries.Where(e => e.UserId == UserId);

        if (parentId != null)
            query = query.Where(e => e.ParentEntryId == parentId);
        else
            query = query.Where(e => e.ParentEntryId == null);

        if (moodId != null)
            query = query.Where(e => e.MoodId == moodId);

        if (categoryId != null)
            query = query.Where(e => e.Categories.Any(c => c.Id == categoryId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            string cleanSearch = search.Trim().ToLower();
            query = query.Where(e => e.Content != null && e.Content.ToLower().Contains(cleanSearch));
        }

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.Type,
                e.MediaPath,
                e.Content,
                e.MoodId,
                e.ParentEntryId,
                e.CreatedAt,
                e.IsEdited,
                e.ReplyToEntryId,
                e.AlbumId,
                CategoryId = e.Categories.Select(c => (Guid?)c.Id).FirstOrDefault(),
                Replies = db.Entries.Count(c => c.ParentEntryId == e.Id)
            })
            .ToListAsync();

        var result = await Task.WhenAll(rows.Select(async r => new EntryDto(
            r.Id, r.Type,
            r.MediaPath == null ? null : await storage.SignAsync(r.MediaPath),
            r.Content, r.MoodId, r.ParentEntryId, r.CreatedAt, r.Replies, r.IsEdited,
            r.ReplyToEntryId, r.CategoryId, r.AlbumId)));

        return result.ToList();
    }

    [HttpGet("all")]
    public async Task<List<EntryDto>> GetAll()
    {
        var rows = await db.Entries
            .Where(e => e.UserId == UserId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.Type,
                e.MediaPath,
                e.Content,
                e.MoodId,
                e.ParentEntryId,
                e.CreatedAt,
                e.IsEdited,
                e.ReplyToEntryId,
                e.AlbumId,
                CategoryId = e.Categories.Select(c => (Guid?)c.Id).FirstOrDefault()
            })
            .ToListAsync();

        var result = await Task.WhenAll(rows.Select(async r => new EntryDto(
            r.Id, r.Type,
            r.MediaPath == null ? null : await storage.SignAsync(r.MediaPath),
            r.Content, r.MoodId, r.ParentEntryId, r.CreatedAt, 0, r.IsEdited,
            r.ReplyToEntryId, r.CategoryId, r.AlbumId)));

        return result.ToList();
    }



    [HttpPost]
    public async Task<ActionResult<EntryDto>> Create(CreateEntryRequest r)
    {
        var existing = await ExistingAsync(r.Id);
        if (existing != null) return existing;

        if (r.Type != EntryType.Text) return BadRequest("Видео отправляется на /api/entries/video");
        var error = await Validate(r.Content, r.MoodId, r.ParentEntryId, r.CategoryId, 5000);
        if (error != null) return BadRequest(error);

        var e = new Entry
        {
            Id = r.Id ?? Guid.NewGuid(),
            UserId = UserId,
            Type = EntryType.Text,
            Content = r.Content!.Trim(),
            ParentEntryId = r.ParentEntryId,
            ReplyToEntryId = r.ReplyToEntryId,
            MoodId = r.MoodId
        };
        await AttachCategory(e, r.CategoryId);
        db.Entries.Add(e);
        await db.SaveChangesAsync();
        return await ToDto(e);
    }

    [HttpPost("video")]
    [RequestSizeLimit(MaxVideoBytes)]
    public async Task<ActionResult<EntryDto>> CreateVideo(
        IFormFile file, [FromForm] string? description,
        [FromForm] short? moodId, [FromForm] Guid? parentEntryId, [FromForm] Guid? id,
        [FromForm] Guid? replyToEntryId, [FromForm] Guid? categoryId)
    {
        var existing = await ExistingAsync(id);
        if (existing != null) return existing;

        if (file.Length == 0 || file.Length > MaxVideoBytes) return BadRequest("Недопустимый размер видео");
        if (file.ContentType != "video/mp4") return BadRequest("Нужен файл mp4");
        var error = await Validate(description, moodId, parentEntryId, categoryId, 200);
        if (error != null) return BadRequest(error);

        var path = $"{UserId}/{Guid.NewGuid()}.mp4";
        await using (var stream = file.OpenReadStream())
            await storage.UploadAsync(path, stream, "video/mp4");

        var e = new Entry
        {
            Id = id ?? Guid.NewGuid(),
            UserId = UserId,
            Type = EntryType.Video,
            MediaPath = path,
            Content = description!.Trim(),
            ParentEntryId = parentEntryId,
            ReplyToEntryId = replyToEntryId,
            MoodId = moodId
        };
        await AttachCategory(e, categoryId);
        db.Entries.Add(e);
        await db.SaveChangesAsync();
        return await ToDto(e);
    }

    [HttpPost("media")]
    [RequestSizeLimit(MaxVideoBytes)]
    public async Task<ActionResult<EntryDto>> CreateMedia(
        IFormFile file, [FromForm] EntryType type, [FromForm] string? description,
        [FromForm] short? moodId, [FromForm] Guid? parentEntryId, [FromForm] Guid? id,
        [FromForm] Guid? replyToEntryId, [FromForm] Guid? categoryId, [FromForm] Guid? albumId)
    {
        var existing = await ExistingAsync(id);
        if (existing != null) return existing;

        if (type != EntryType.Audio && type != EntryType.File && type != EntryType.Photo)
            return BadRequest("Неверный тип");
        if (file.Length == 0 || file.Length > MaxVideoBytes) return BadRequest("Недопустимый размер файла");
        if (type == EntryType.Audio && !file.ContentType.StartsWith("audio/")) return BadRequest("Нужен аудиофайл");
        if (type == EntryType.Photo && !AllowedImageTypes.Contains(file.ContentType)) return BadRequest("Нужно изображение");
        if (description is { Length: > 200 }) return BadRequest("Слишком длинное описание");
        var error = await ValidateRefs(moodId, parentEntryId, categoryId);
        if (error != null) return BadRequest(error);

        var safeName = new string(Path.GetFileName(file.FileName)
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray());
        if (safeName.Length > 80) safeName = safeName[^80..];
        var path = $"{UserId}/{Guid.NewGuid()}/{safeName}";

        var contentType = type is EntryType.Audio or EntryType.Photo ? file.ContentType : "application/octet-stream";
        await using (var stream = file.OpenReadStream())
            await storage.UploadAsync(path, stream, contentType);

        var e = new Entry
        {
            Id = id ?? Guid.NewGuid(),
            UserId = UserId,
            Type = type,
            MediaPath = path,
            Content = description?.Trim(),
            ParentEntryId = parentEntryId,
            ReplyToEntryId = replyToEntryId,
            AlbumId = albumId,
            MoodId = moodId
        };
        await AttachCategory(e, categoryId);
        db.Entries.Add(e);
        await db.SaveChangesAsync();
        return await ToDto(e);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EntryDto>> Update(Guid id, UpdateEntryRequest r)
    {
        var e = await db.Entries.Include(x => x.Categories)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (e == null) return NotFound();

        var text = r.Content?.Trim();
        if (e.Type == EntryType.Text && string.IsNullOrWhiteSpace(text)) return BadRequest("Пустой текст");
        var maxLen = e.Type == EntryType.Text ? 5000 : 200;
        if (text != null && text.Length > maxLen) return BadRequest("Слишком длинный текст");
        if (r.MoodId is short mid && !await db.Moods.AnyAsync(m => m.Id == mid)) return BadRequest("Такой эмоции нет");

        if (e.Categories.FirstOrDefault()?.Id != r.CategoryId)
        {
            e.Categories.Clear();
            if (r.CategoryId is Guid cid)
            {
                var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == UserId);
                if (cat == null) return BadRequest("Категория не найдена");
                e.Categories.Add(cat);
            }
        }

        if (e.Type != EntryType.Audio) e.Content = text;
        e.MoodId = r.MoodId;
        if (r.IsEdited) e.IsEdited = true;
        await db.SaveChangesAsync();
        return await ToDto(e);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (e == null) return NotFound();

        var paths = await db.Entries
            .Where(x => x.UserId == UserId && (x.Id == id || x.ParentEntryId == id) && x.MediaPath != null)
            .Select(x => x.MediaPath!)
            .ToListAsync();

        db.Entries.Remove(e);
        await db.SaveChangesAsync();

        foreach (var p in paths)
        {
            try { await storage.DeleteAsync(p); }
            catch {  }
        }
        return NoContent();
    }


    private async Task<EntryDto> ToDto(Entry e)
    {
        var cats = db.Entry(e).Collection(x => x.Categories);
        if (!cats.IsLoaded) await cats.LoadAsync();
        return new EntryDto(
            e.Id, e.Type, e.MediaPath == null ? null : await storage.SignAsync(e.MediaPath),
            e.Content, e.MoodId, e.ParentEntryId, e.CreatedAt,
            await db.Entries.CountAsync(c => c.ParentEntryId == e.Id), e.IsEdited,
            e.ReplyToEntryId, e.Categories.FirstOrDefault()?.Id, e.AlbumId);
    }

    private async Task<EntryDto?> ExistingAsync(Guid? id)
    {
        if (id is not Guid gid) return null;
        var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == gid && x.UserId == UserId);
        return e == null ? null : await ToDto(e);
    }

    private async Task AttachCategory(Entry e, Guid? categoryId)
    {
        if (categoryId is not Guid cid) return;
        e.Categories.Add(await db.Categories.FirstAsync(c => c.Id == cid && c.UserId == UserId));
    }

    private async Task<string?> ValidateRefs(short? moodId, Guid? parentId, Guid? categoryId)
    {
        if (moodId is short mid && !await db.Moods.AnyAsync(m => m.Id == mid)) return "Такой эмоции нет";
        if (parentId is Guid pid &&
            !await db.Entries.AnyAsync(p => p.Id == pid && p.UserId == UserId && p.ParentEntryId == null))
            return "Родительская запись не найдена";
        if (categoryId is Guid cid &&
            !await db.Categories.AnyAsync(c => c.Id == cid && c.UserId == UserId))
            return "Категория не найдена";
        return null;
    }

    private async Task<string?> Validate(string? text, short? moodId, Guid? parentId, Guid? categoryId, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Пустой текст";
        if (text.Length > maxLen) return "Слишком длинный текст";
        return await ValidateRefs(moodId, parentId, categoryId);
    }
}