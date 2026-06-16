using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;
using DevHabit.Api.DTOs.Common;

namespace DevHabit.Api.Services;

public sealed class DataShapingService
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache = new(); 
    public ExpandoObject ShapeData<T>(T entity, string? fields)
    {
        HashSet<string> fieldsSet = fields?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        PropertyInfo[] propertyInfos = _propertiesCache.GetOrAdd(typeof(T), type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        if (fieldsSet.Any())
        {
            propertyInfos = propertyInfos.Where(p => fieldsSet.Contains(p.Name)).ToArray();
        }
        IDictionary<string, object?> shapedObject = new ExpandoObject();
        foreach (PropertyInfo propertyInfo in propertyInfos)
        {
            object? propertyValue = propertyInfo.GetValue(entity);
            shapedObject[propertyInfo.Name] = propertyValue;
        }
        return (ExpandoObject)shapedObject;
    }
    public List<ExpandoObject> ShapeCollectionData<T>(IEnumerable<T> entities, string? fields, Func<T, List<LinkDto>>? linksFactory = null)
    {
        HashSet<string> fieldsSet = fields?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        PropertyInfo[] propertyInfos = _propertiesCache.GetOrAdd(typeof(T), type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        if (fieldsSet.Any())
        {
            propertyInfos = propertyInfos.Where(p => fieldsSet.Contains(p.Name)).ToArray();
        }
        List<ExpandoObject> shapedObjects = [];
        foreach (T entity in entities)
        {
            IDictionary<string, object?> shapedObject = new ExpandoObject();
            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                object? propertyValue = propertyInfo.GetValue(entity);
                shapedObject[propertyInfo.Name] = propertyValue;
            }
            if (linksFactory is not null)
            {
                shapedObject["links"] = linksFactory(entity);
            }
            shapedObjects.Add((ExpandoObject)shapedObject);
        }
        return shapedObjects;
    }
    public bool Validate<T>(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return true;
        }
        var fieldsSet = fields.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        PropertyInfo[] propertyInfos = _propertiesCache.GetOrAdd(typeof(T), type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        return fieldsSet.All(f => propertyInfos.Any(p => p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));
        
    }
}
