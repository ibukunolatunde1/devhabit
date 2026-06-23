using DevHabit.Api.Entities;
using DevHabit.Api.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using DevHabit.Api.Services;

namespace DevHabit.Api.DTOs.Habits;

public record AcceptHeaderDto
{
    [FromHeader(Name = "Accept")]
    public string? Accept { get; init; }

    public bool IncludeLinks =>
        MediaTypeHeaderValue.TryParse(Accept, out MediaTypeHeaderValue? mediaType) &&
        mediaType.SubTypeWithoutSuffix.HasValue &&
        mediaType.SubTypeWithoutSuffix.Value.Contains(CustomMediaTypeNames.Application.HateoasSubType);
}
