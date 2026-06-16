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

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HabitsController(ApplicationDbContext dbContext, LinkService linkService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHabits(
        [FromQuery] HabitQueryParameters queryParameters, 
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
        bool includeLinks = queryParameters.Accept == CustomMediaTypeNames.HateoasJson;
        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(items, queryParameters.Fields, includeLinks ? h => CreateLinksForHabits(h.Id, queryParameters.Fields) : null),
            Page = queryParameters.Page,
            PageSize = queryParameters.PageSize,
            TotalCount = totalCount
        };
        if (includeLinks)
        {
            paginationResult.Links = CreateLinksForHabits(queryParameters, paginationResult.HasNextPage, paginationResult.HasPreviousPage);
        }
        // PaginationResult<HabitDto> paginationResult = await PaginationResult<HabitDto>.CreateAsync(habitsQuery, queryParameters.Page, queryParameters.PageSize);
        return Ok(paginationResult);
    }

    

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHabit(string id, string? fields, DataShapingService dataShapingService, [FromHeader(Name = "Accept")] string? accept)
    {
        if (!dataShapingService.Validate<HabitWithTagsDto>(fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided fields parameter '{fields}' is invalid."
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
        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, fields);
        if (accept == CustomMediaTypeNames.HateoasJson)
        {
            List<LinkDto> links = CreateLinksForHabits(id, fields);
            shapedHabitDto.TryAdd("links", links);
        }
        return Ok(shapedHabitDto);
    }

    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(CreateHabitDto createHabitDto, IValidator<CreateHabitDto> validator)
    {
        await validator.ValidateAndThrowAsync(createHabitDto);
        Habit habit = createHabitDto.ToEntity();
        dbContext.Habits.Add(habit);
        await dbContext.SaveChangesAsync();
        HabitDto habitDto = habit.ToDto();
        habitDto.Links = CreateLinksForHabits(habitDto.Id, null).ToList();
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

    private List<LinkDto> CreateLinksForHabits(HabitQueryParameters queryParameters, bool hasNextPage, bool hasPreviousPage)
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
