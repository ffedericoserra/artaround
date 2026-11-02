using Microsoft.AspNetCore.Mvc;
using ArtAround.Api.Repositories;
using ArtAround.Api.Dtos;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Controllers;

[ApiController]
[Route("api/museums")]
public class MuseumsController(IMuseumRepository repo) : ControllerBase
{
    private const string NotFoundMessage = "Museum not found";

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var museums = await repo.ListAsync();
        var dtos = museums.Select(MuseumSummaryDto.From).ToList();
        return Ok(new { museums = dtos });
    }

    [HttpGet("{idOrSlug}")]
    public async Task<IActionResult> Get(string idOrSlug)
    {
        var museum = await repo.FindAsync(idOrSlug);
        return museum is null
            ? NotFound(new { error = NotFoundMessage })
            : Ok(new { museum = MuseumDto.From(museum) });
    }

    [HttpGet("{idOrSlug}/contents")]
    public async Task<IActionResult> GetContents(string idOrSlug, string? type)
    {
        ContentType? filter = null;
        if (Request.Query.ContainsKey("type"))
        {
            if (type is null || !Enum.GetNames<ContentType>().Contains(type))
            {
                return BadRequest(new { error = "Invalid content type" });
            }
            filter = Enum.Parse<ContentType>(type);
        }

        var contents = await repo.ListContentsAsync(idOrSlug, filter);
        return contents is null
            ? NotFound(new { error = NotFoundMessage })
            : Ok(new { contents = contents.Select(ContentDto.From).ToList() });
    }
}