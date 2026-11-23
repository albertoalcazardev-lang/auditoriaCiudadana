using FluentValidation.Results;

namespace VyTSystems.Application.Common.Validation;

public static class ValidationExtensions
{
    public static IReadOnlyList<ValidationError> ToValidationErrors(
        this IEnumerable<ValidationFailure> failures) =>
        failures
            .Select(failure => new ValidationError(failure.PropertyName, failure.ErrorMessage))
            .ToArray();

    public static IDictionary<string, string[]> ToDictionary(
        this IEnumerable<ValidationError> errors) =>
        errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
}
