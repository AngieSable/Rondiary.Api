using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rondiary.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Rondiary.Api.Controllers;

public record CategoryDto(Guid Id, string Name);
public record CreateCategoryRequest(Guid? Id, string Name);


[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<List<CategoryDto>> Get() =>
        await db.Categories.Where(c => c.UserId == UserId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest r)
    {
        if (r.Id is Guid gid)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Id == gid && c.UserId == UserId);
            if (existing != null) return new CategoryDto(existing.Id, existing.Name);
        }

        var name = r.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 30)
            return BadRequest("Название от 1 до 30 символов");
        if (await db.Categories.AnyAsync(c => c.UserId == UserId && c.Name == name))
            return Conflict("Такая категория уже есть");

        var cat = new Category { Id = r.Id ?? Guid.NewGuid(), UserId = UserId, Name = name };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return new CategoryDto(cat.Id, cat.Name);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (cat == null) return NotFound();
        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}