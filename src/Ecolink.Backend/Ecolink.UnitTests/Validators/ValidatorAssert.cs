using FluentValidation;

namespace Ecolink.UnitTests.Validators;

/// <summary>Helper dùng chung cho các test validator (không phụ thuộc DB).</summary>
internal static class ValidatorAssert
{
    /// <summary>true nếu validator báo lỗi đúng thuộc tính <paramref name="prop"/>.</summary>
    public static bool Has<T>(IValidator<T> v, T model, string prop)
        => v.Validate(model).Errors.Any(e => e.PropertyName == prop);

    public static bool IsValid<T>(IValidator<T> v, T model) => v.Validate(model).IsValid;

    public static string Str(int length, char c = 'a') => new(c, length);
}
