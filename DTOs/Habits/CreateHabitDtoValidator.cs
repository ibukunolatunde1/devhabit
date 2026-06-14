using DevHabit.Api.Entities;
using FluentValidation;

namespace DevHabit.Api.DTOs.Habits;

public sealed class CreateHabitDtoValidator : AbstractValidator<CreateHabitDto>
{
    private static readonly string [] AllowedUnits = ["minutes", "hours", "steps", "km", "cal", "pages", "books", "tasks", "sessions"];
    private static readonly string [] AllowedUnitsForBinaryHabits = ["sessions", "units"];

    public CreateHabitDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3).WithMessage("Name is required and must be at least 3 characters long.");
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null).WithMessage("Description cannot exceed 500 characters.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Type must be a valid HabitType.");
        RuleFor(x => x.Frequency.Type).IsInEnum().WithMessage("Frequency type must be a valid FrequencyType.");
        RuleFor(x => x.Frequency.TimesPerPeriod).GreaterThan(0).WithMessage("TimesPerPeriod must be greater than 0.");
        RuleFor(x => x.Target.Value).GreaterThan(0).WithMessage("Target value must be greater than 0.");
        RuleFor(x => x.Target.Unit)
            .NotEmpty()
            .Must(unit => AllowedUnits.Contains(unit.ToLowerInvariant()))
            .WithMessage($"Target unit must be one of the following: {string.Join(", ", AllowedUnits)}.");
        RuleFor(x => x.EndDate).Must(date => date is null || date.Value > DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("EndDate must be in the future.");
        When( x => x.Milestone is not null, () =>
        {
            RuleFor(x => x.Milestone!.Target)
                .GreaterThan(0)
                .WithMessage("Milestone target value must be greater than 0.");
        });
        RuleFor(x => x.Target.Unit)
            .Must((dto, unit) => IsTargetUnitCompatibleWithType(dto.Type, unit))
            .WithMessage("Target unit is not compatible with the habit type.");
    }

    private static bool IsTargetUnitCompatibleWithType(HabitType habitType, string unit)
    {
        string normalizedUnit = unit.ToLowerInvariant();
        return habitType switch
        {
            HabitType.Binary => AllowedUnitsForBinaryHabits.Contains(normalizedUnit),
            HabitType.Measurable => AllowedUnits.Contains(normalizedUnit),
            _ => false
        };
    }
}
