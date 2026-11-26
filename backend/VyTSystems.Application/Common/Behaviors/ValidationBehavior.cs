using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace VyTSystems.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ValidationFailure> failures = [];
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            foreach (var failure in result.Errors)
            {
                // Los valores rechazados pueden contener credenciales; no llevarlos a la excepción.
                failures.Add(new ValidationFailure(failure.PropertyName, failure.ErrorMessage)
                {
                    ErrorCode = failure.ErrorCode,
                    Severity = failure.Severity
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
