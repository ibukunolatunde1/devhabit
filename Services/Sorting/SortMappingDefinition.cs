namespace DevHabit.Api.Services.Sorting;

public sealed class SortMappingDefinition<TSource, TDestination> : ISortMappingDefinition
{
    public required SortMapping[] Mappings { get; init; }

    public Type SourceType => typeof(TSource);

    public Type DestinationType => typeof(TDestination);
}
