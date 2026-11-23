namespace VyTSystems.Application.Common.Validation;

/// <summary>
/// Representa un error de validación de entrada asociado a una propiedad específica.
/// </summary>
public sealed record ValidationError(string PropertyName, string ErrorMessage);
