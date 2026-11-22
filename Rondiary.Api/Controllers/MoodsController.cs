using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rondiary.Api.Models;

namespace Rondiary.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class MoodsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<Mood>> Get() => await db.Moods.OrderBy(m => m.Id).ToListAsync();
}
