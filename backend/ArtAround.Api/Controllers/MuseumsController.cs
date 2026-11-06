using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArtAround.Api.Repositories;
using ArtAround.Api.Dtos;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;

namespace ArtAround.Api.Controllers;

[AllowAnonymous]
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
            ? ApiProblem.NotFound(NotFoundMessage)
            : Ok(new { museum = MuseumDto.From(museum) });
    }

    [HttpGet("{idOrSlug}/contents")]
    public async Task<IActionResult> GetContents(string idOrSlug)
    {
        // Read the raw values: ?type= gives [""], a repeated type gives two values, no type gives none.
        var values = Request.Query["type"];
        ContentType? filter = null;
        if (values.Count > 0)
        {
            var type = values[0];
            if (values.Count > 1 || type is null || !Enum.GetNames<ContentType>().Contains(type))
            {
                return ApiProblem.BadRequest("Invalid content type");
            }
            filter = Enum.Parse<ContentType>(type);
        }

        var contents = await repo.ListContentsAsync(idOrSlug, filter);
        return contents is null
            ? ApiProblem.NotFound(NotFoundMessage)
            : Ok(new { contents = contents.Select(ContentDto.From).ToList() });
    }

    [HttpGet("{idOrSlug}/visits")]
    public async Task<IActionResult> GetVisits(string idOrSlug)
    {
        var visits = await repo.ListVisitsAsync(idOrSlug, User.FindId());
        return visits is null
            ? ApiProblem.NotFound(NotFoundMessage)
            : Ok(new { visits = visits.Select(VisitSummaryDto.From).ToList() });
    }
}
