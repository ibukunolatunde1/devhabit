using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.DTOs.Habits;

public sealed record HabitQueryParameters : AcceptHeaderDto
{
    public string? Fields { get; init; }
}
