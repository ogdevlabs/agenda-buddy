using System.ComponentModel;
using System.Reflection;

namespace Library.Tools;

public static class EnumHelper<TEnum> where TEnum : Enum 
{
    public static string GetEnumDescription(Enum value)
    {
        FieldInfo field = value.GetType().GetField(value.ToString());
        DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();

        return attribute == null ? value.ToString() : attribute.Description;
    }

    public static T GetEnumValueFromDescription<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                {
                    return (T)field.GetValue(null);
                }
            }
            else
            {
                if (field.Name == description)
                {
                    return (T)field.GetValue(null);
                }
            }
        }

        throw new ArgumentException($"No enum value with description {description} found.");
    }
    
    public static void SaveEnumDescription(TEnum enumValue)
    {
        string description = EnumHelper<TEnum>.GetEnumDescription(enumValue);
    }
    
    public static TEnum RetrieveEnumFromDescription(string description)
    {
        return EnumHelper<TEnum>.GetEnumValueFromDescription<TEnum>(description);
    }
}