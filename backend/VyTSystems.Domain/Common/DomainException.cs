namespace VyTSystems.Domain.Common;

/// <summary>
/// Excepción base para representar violaciones a invariantes o reglas fundamentales del dominio.
/// </summary>
public class DomainException(string message) : Exception(message);
