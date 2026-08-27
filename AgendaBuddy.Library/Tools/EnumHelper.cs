namespace AgendaBuddy.Library.Tools;

public static class EnumHelper<TEnum> where TEnum : Enum
{
    public static string GetEnumDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString())!;
        var attribute = field.GetCustomAttribute<DescriptionAttribute>()!;

        return attribute == null ? value.ToString() : attribute.Description;
    }

    public static T GetEnumValueFromDescription<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                {
#pragma warning disable CS8600
                    // Converting null literal or possible null value to non-nullable type.
                    return ((T)field.GetValue(null))!;
#pragma warning restore CS8600
                    // Converting null literal or possible null value to non-nullable type.
                }
            }
            else
            {
                if (field.Name == description)
                {
#pragma warning disable CS8600
                    // Converting null literal or possible null value to non-nullable type.
                    return ((T)field.GetValue(null))!;
#pragma warning restore CS8600
                    // Converting null literal or possible null value to non-nullable type.
                }
            }

        throw new ArgumentException($"No enum value with description {description} found.");
    }

    public static void SaveEnumDescription(TEnum enumValue)
    {
        var description = GetEnumDescription(enumValue);
    }

    public static TEnum RetrieveEnumFromDescription(string description)
    {
        return GetEnumValueFromDescription<TEnum>(description);
    }
}
