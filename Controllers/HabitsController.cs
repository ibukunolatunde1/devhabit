using DevHabit.Api.Database;
using DevHabit.Api.Entities;
using DevHabit.Api.DTOs.Habits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Microsoft.AspNetCore.JsonPatch;
using FluentValidation;
using FluentValidation.Results;
using System.Linq.Dynamic.Core;
using DevHabit.Api.Services.Sorting;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Services;
using System.Dynamic;
using Asp.Versioning;
using System.Net.Mime;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.JsonV2,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1,
    CustomMediaTypeNames.Application.HateoasJsonV2
)]
public sealed class HabitsController(ApplicationDbContext dbContext, LinkService linkService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHabits(
        [FromQuery] HabitsQueryParameters queryParameters, 
        SortMappingProvider sortMappingProvider, 
        DataShapingService dataShapingService
        )
    {
        if(!sortMappingProvider.ValidateMappings<HabitDto, Habit>(queryParameters.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter '{queryParameters.Sort}' is invalid."
            );
        }
        if(!dataShapingService.Validate<HabitDto>(queryParameters.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter '{queryParameters.Fields}' is invalid."
            );
        }
        string? search = queryParameters.Search?.Trim().ToLowerInvariant();
        string pattern = $"%{search}%";
        SortMapping[] sortMappings = sortMappingProvider.GetMappings<HabitDto, Habit>();
        
        IQueryable<HabitDto> habitsQuery = dbContext.Habits
            .Where(h => EF.Functions.ILike(h.Name, pattern) || h.Description != null && EF.Functions.ILike(h.Description, pattern))
            .Where(h => queryParameters.Type == null || h.Type == queryParameters.Type)
            .Where(h => queryParameters.Status == null || h.Status == queryParameters.Status)
            .ApplySort(queryParameters.Sort, sortMappings)
            .Select(HabitQueries.ProjectToDto());

        int totalCount = await habitsQuery.CountAsync();
        List<HabitDto> items = await habitsQuery.Skip((queryParameters.Page - 1) * queryParameters.PageSize).Take(queryParameters.PageSize).ToListAsync();
        
        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(items, queryParameters.Fields, queryParameters.IncludeLinks ? h => CreateLinksForHabits(h.Id, queryParameters.Fields) : null),
            Page = queryParameters.Page,
            PageSize = queryParameters.PageSize,
            TotalCount = totalCount
        };
        if (queryParameters.IncludeLinks)
        {
            paginationResult.Links = CreateLinksForHabits(queryParameters, paginationResult.HasNextPage, paginationResult.HasPreviousPage);
        }
        return Ok(paginationResult);
    }

    

    [HttpGet("{id}")]
    [MapToApiVersion(1.0)]
    public async Task<IActionResult> GetHabit(string id, [FromQuery] HabitQueryParameters queryParameters, DataShapingService dataShapingService)
    {
        if (!dataShapingService.Validate<HabitWithTagsDto>(queryParameters.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter '{queryParameters.Fields}' is invalid."
            );
        }

        HabitWithTagsDto? habit = await dbContext
        .Habits
        .Where(habit => habit.Id == id)
        .Select(HabitQueries.ProjectToDtoWithTags()).FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }
        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, queryParameters.Fields);
        if (queryParameters.IncludeLinks)
        {
            List<LinkDto> links = CreateLinksForHabits(id, queryParameters.Fields);
            shapedHabitDto.TryAdd("links", links);
        }
        return Ok(shapedHabitDto);
    }

    [HttpGet("{id}")]
    [MapToApiVersion(2.0)]
    public async Task<IActionResult> GetHabitV2(string id, [FromQuery] HabitQueryParameters queryParameters, DataShapingService dataShapingService)
    {
        if (!dataShapingService.Validate<HabitWithTagsDtoV2>(queryParameters.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter '{queryParameters.Fields}' is invalid."
            );
        }

        HabitWithTagsDtoV2? habit = await dbContext
        .Habits
        .Where(habit => habit.Id == id)
        .Select(HabitQueries.ProjectToDtoWithTagsV2()).FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }
        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, queryParameters.Fields);
        if (queryParameters.IncludeLinks)
        {
            List<LinkDto> links = CreateLinksForHabits(id, queryParameters.Fields);
            shapedHabitDto.TryAdd("links", links);
        }
        return Ok(shapedHabitDto);
    }

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(CreateHabitDto createHabitDto, [FromHeader] AcceptHeaderDto acceptHeader, IValidator<CreateHabitDto> validator)
    {
        await validator.ValidateAndThrowAsync(createHabitDto);
        Habit habit = createHabitDto.ToEntity();
        dbContext.Habits.Add(habit);
        await dbContext.SaveChangesAsync();
        HabitDto habitDto = habit.ToDto();
        if (acceptHeader.IncludeLinks)
        {
            habitDto.Links = CreateLinksForHabits(habitDto.Id, null);
        }
        return CreatedAtAction(nameof(GetHabit), new { id = habitDto.Id }, habitDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, [FromBody] UpdateHabitDto updateHabitDto)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);
        if (habit is null)
        {
            return NotFound();
        }
        habit.UpdateFromDto(updateHabitDto);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult> PatchHabit(string id, JsonPatchDocument<HabitDto> patchDocument)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);
        if (habit is null)
        {
            return NotFound();
        }
        HabitDto habitDto = habit.ToDto();
        patchDocument.ApplyTo(habitDto, ModelState);
        if (!TryValidateModel(habitDto))
        {
            return ValidationProblem(ModelState);
        }
        habit.Name = habitDto.Name;
        habit.Description = habitDto.Description;
        habit.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task <ActionResult> DeleteHabit(string id)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);
        if(habit is null)
        {
            return NotFound();
            // return StatusCode(StatusCodes.Status410Gone);
        }
        dbContext.Habits.Remove(habit);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    private List<LinkDto> CreateLinksForHabits(HabitsQueryParameters queryParameters, bool hasNextPage, bool hasPreviousPage)
    {
        List<LinkDto> links = [
            linkService.Create(nameof(GetHabits), "self", HttpMethods.Get, new
            {
                page = queryParameters.Page,
                pageSize = queryParameters.PageSize,
                q = queryParameters.Search,
                type = queryParameters.Type,
                status = queryParameters.Status,
                sort = queryParameters.Sort,
                fields = queryParameters.Fields
            }),
            linkService.Create(nameof(CreateHabit), "create", HttpMethods.Post)
        ];
        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "nextPage", HttpMethods.Get, new
            {
                page = queryParameters.Page + 1,
                pageSize = queryParameters.PageSize,
                q = queryParameters.Search,
                type = queryParameters.Type,
                status = queryParameters.Status,
                sort = queryParameters.Sort,
                fields = queryParameters.Fields
            }));
        }
        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "previousPage", HttpMethods.Get, new
            {
                page = queryParameters.Page - 1,
                pageSize = queryParameters.PageSize,
                q = queryParameters.Search,
                type = queryParameters.Type,
                status = queryParameters.Status,
                sort = queryParameters.Sort,
                fields = queryParameters.Fields
            }));
        }
        return links;
    }

    private List<LinkDto> CreateLinksForHabits(string id, string? fields)
    {
        return new List<LinkDto>
        {
            linkService.Create(nameof(GetHabit), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateHabit), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(PatchHabit), "patch", HttpMethods.Patch, new { id }),
            linkService.Create(nameof(DeleteHabit), "delete", HttpMethods.Delete, new { id }),
            linkService.Create(nameof(HabitTagsController.UpsertHabitTags), "upsertTags", HttpMethods.Put, new { habitId = id }, HabitTagsController.Name)
        };
    }

}
