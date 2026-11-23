namespace VyTSystems.Application.Common.CQRS;

/// <summary>
/// Representa un comando que modifica el estado del sistema sin devolver un valor adicional.
/// </summary>
public interface ICommand;

/// <summary>
/// Representa un comando que modifica el estado del sistema y devuelve un valor de tipo TResult.
/// </summary>
public interface ICommand<TResult>;
