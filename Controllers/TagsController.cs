using System.Net.Mime;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1
)]
public sealed class TagsController(ApplicationDbContext dbContext, LinkService linkService, UserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TagsCollectionDto>> GetTags([FromHeader] AcceptHeaderDto acceptHeader)
    {
        string? userId = await userContext.GetUserIdAsync();
        if(string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User is not authenticated."
            );
        }
        List<TagDto> tags = await dbContext
            .Tags
            .Select(TagQueries.ProjectToDto())
            .ToListAsync();

        var tagsCollectionDto = new TagsCollectionDto
        {
            Items = tags
        };

        if (acceptHeader.IncludeLinks)
        {
            tagsCollectionDto.Links = CreateLinksForTags();
        }

        return Ok(tagsCollectionDto);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TagDto>> GetTag(string id, [FromHeader] AcceptHeaderDto acceptHeader)
    {
        string? userId = await userContext.GetUserIdAsync();
        if(string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User is not authenticated."
            );
        }
        TagDto? tag = await dbContext
            .Tags
            .Where(h => h.Id == id && h.UserId == userId)
            .Select(TagQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (tag is null)
        {
            return NotFound();
        }

        if (acceptHeader.IncludeLinks)
        {
            tag.Links = CreateLinksForTag(tag.Id);
        }

        return Ok(tag);
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> CreateTag(CreateTagDto createTagDto, [FromHeader] AcceptHeaderDto acceptHeader, IValidator<CreateTagDto> validator, ProblemDetailsFactory problemDetailsFactory)
    {
        string? userId = await userContext.GetUserIdAsync();
        if(string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User is not authenticated."
            );
        }
        ValidationResult validationResult = await validator.ValidateAsync(createTagDto);
        if (!validationResult.IsValid)
        {
            ProblemDetails problem = problemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest
            );
            problem.Extensions.Add("errors", validationResult.ToDictionary());
            //return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
            return BadRequest(problem);
        }
        Tag tag = createTagDto.ToEntity(userId);

        if (await dbContext.Tags.AnyAsync(t => t.Name == tag.Name))
        {
            return Problem(
                detail: $"A tag with the name '{tag.Name}' already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        dbContext.Tags.Add(tag);

        await dbContext.SaveChangesAsync();

        TagDto tagDto = tag.ToDto();
        if (acceptHeader.IncludeLinks)
        {
            tagDto.Links = CreateLinksForTag(tagDto.Id);
        }

        return CreatedAtAction(nameof(GetTag), new { id = tagDto.Id }, tagDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateTag(string id, UpdateTagDto updateTagDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if(string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User is not authenticated."
            );
        }
        
        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (tag is null)
        {
            return NotFound();
        }

        tag.UpdateFromDto(updateTagDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTag(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if(string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "User is not authenticated."
            );
        }

        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (tag is null)
        {
            return NotFound();
        }

        dbContext.Tags.Remove(tag);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private List<LinkDto> CreateLinksForTags()
    {
        List<LinkDto> links = [
            linkService.Create(nameof(GetTags), "self", HttpMethods.Get),
            linkService.Create(nameof(CreateTag), "create", HttpMethods.Post)
        ];
        return links;
    }

    private List<LinkDto> CreateLinksForTag(string id)
    {
        List<LinkDto> links = [
            linkService.Create(nameof(GetTag), "self", HttpMethods.Get, new { id }),
            linkService.Create(nameof(UpdateTag), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(DeleteTag), "delete", HttpMethods.Delete, new { id })
        ];
        return links;
    }
}
