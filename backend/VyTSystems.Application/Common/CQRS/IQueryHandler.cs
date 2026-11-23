using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Common.CQRS;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
