using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Common.CQRS;

public interface ICommandDispatcher
{
    Task<Result> SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    Task<Result<TResult>> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>;
}

public interface IQueryDispatcher
{
    Task<Result<TResult>> QueryAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>;
}
