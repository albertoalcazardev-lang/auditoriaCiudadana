namespace VyTSystems.Application.Abstractions.Authentication;

/// <summary>Modo solicitado; no acredita la pertenencia a un rol.</summary>
public enum RequestedAccess
{
    Administrador = 1,
    Consulta = 2
}
