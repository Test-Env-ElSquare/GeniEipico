using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations;

internal sealed class EnumStringValueConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public EnumStringValueConverter()
        : base(ToProviderExpression(), FromProviderExpression())
    {
    }

    private static Expression<Func<TEnum, string>> ToProviderExpression()
        => value => ToStorageValue(value);

    private static Expression<Func<string, TEnum>> FromProviderExpression()
        => value => FromStorageValue(value);

    private static string ToStorageValue(TEnum value)
    {
        var name = value.ToString();
        var characters = new List<char>(name.Length + 4);

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character))
            {
                characters.Add('_');
            }

            characters.Add(char.ToLowerInvariant(character));
        }

        return new string(characters.ToArray());
    }

    private static TEnum FromStorageValue(string value)
    {
        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            var enumName = enumValue.ToString().Replace("_", string.Empty);
            if (string.Equals(enumName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return enumValue;
            }
        }

        throw new InvalidOperationException(
            $"Database value '{value}' is not valid for enum {typeof(TEnum).Name}.");
    }
}
