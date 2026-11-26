using VyTSystems.Application.Common.Validation;

namespace VyTSystems.Application.Common.Results;

public enum ErrorType
{
    None = 0,
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5
}

public sealed record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);
}

/// <summary>
/// Resultado tipado estándar para operaciones CQRS (éxito, error funcional o errores de FluentValidation).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    protected Result(bool isSuccess, Error error, IReadOnlyList<ValidationError>? validationErrors = null)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Un resultado exitoso no puede contener un error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Un resultado fallido debe especificar un error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        ValidationErrors = validationErrors ?? [];
    }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result ValidationFailure(IReadOnlyList<ValidationError> validationErrors) =>
        new(
            false,
            Error.Validation("Validation.Failed", "Uno o más errores de validación ocurrieron."),
            validationErrors);

    public static Result<TValue> Success<TValue>(TValue value) =>
        new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) =>
        new(default, false, error);

    public static Result<TValue> ValidationFailure<TValue>(IReadOnlyList<ValidationError> validationErrors) =>
        new(
            default,
            false,
            Error.Validation("Validation.Failed", "Uno o más errores de validación ocurrieron."),
            validationErrors);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder a Value cuando el resultado es fallido.");

    internal Result(
        TValue? value,
        bool isSuccess,
        Error error,
        IReadOnlyList<ValidationError>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
    {
        _value = value;
    }
}
