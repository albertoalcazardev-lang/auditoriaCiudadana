namespace VyTSystems.Domain.Enums;

/// <summary>
/// Estatus efectivo de la solicitud considerando la fecha de vencimiento respecto al día vigente.
/// </summary>
public enum EstatusEfectivo
{
    EnProceso = 1,
    Entregada = 2,
    Vencida = 3
}
