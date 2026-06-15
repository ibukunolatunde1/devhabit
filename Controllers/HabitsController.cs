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
public sealed class HabitsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHabits([FromQuery] HabitQueryParameters queryParameters, SortMappingProvider sortMappingProvider, DataShapingService dataShapingService)
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
            Items = dataShapingService.ShapeCollectionData(items, queryParameters.Fields),
            Page = queryParameters.Page,
            PageSize = queryParameters.PageSize,
            TotalCount = totalCount
        };
        // PaginationResult<HabitDto> paginationResult = await PaginationResult<HabitDto>.CreateAsync(habitsQuery, queryParameters.Page, queryParameters.PageSize);
        return Ok(paginationResult);
    }

    

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitWithTagsDto>> GetHabit(string id, string? fields, DataShapingService dataShapingService)
    {
        if(!dataShapingService.Validate<HabitWithTagsDto>(fields))
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
}
