using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Common.Validation;

namespace VyTSystems.Application.Common.CQRS;

/// <summary>
/// Despachador único de Commands y Queries que ejecuta todos los validadores de FluentValidation
/// registrados antes de invocar el Handler correspondiente.
/// </summary>
public sealed class RequestDispatcher(IServiceProvider serviceProvider)
    : ICommandDispatcher, IQueryDispatcher
{
    public async Task<Result> SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationErrors = await ValidateAsync(command, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return Result.ValidationFailure(validationErrors);
        }

        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand>>();
        return await handler.HandleAsync(command, cancellationToken);
    }

    public async Task<Result<TResult>> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationErrors = await ValidateAsync(command, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return Result.ValidationFailure<TResult>(validationErrors);
        }

        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return await handler.HandleAsync(command, cancellationToken);
    }

    public async Task<Result<TResult>> QueryAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(query);

        var validationErrors = await ValidateAsync(query, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return Result.ValidationFailure<TResult>(validationErrors);
        }

        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, cancellationToken);
    }

    private async Task<IReadOnlyList<ValidationError>> ValidateAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken)
    {
        var validators = serviceProvider.GetServices<IValidator<TRequest>>().ToArray();
        if (validators.Length == 0)
        {
            return [];
        }

        var context = new ValidationContext<TRequest>(request);
        var errors = new List<ValidationError>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(context, cancellationToken);
            if (!validationResult.IsValid)
            {
                errors.AddRange(validationResult.Errors.ToValidationErrors());
            }
        }

        return errors;
    }
}
