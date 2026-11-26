namespace VyTSystems.Application.Dashboard.ObtenerResumen;

/// <summary>
/// Catálogo cerrado de identificadores canónicos de periodo soportados por el dashboard administrativo.
/// </summary>
public static class DashboardPeriodos
{
    public const string EsteMes = "este-mes";
    public const string MesAnterior = "mes-anterior";
    public const string Ultimos3Meses = "ultimos-3-meses";
    public const string EsteAnio = "este-anio";
    public const string Todo = "todo";

    private static readonly HashSet<string> PeriodosValidos = new(StringComparer.Ordinal)
    {
        EsteMes,
        MesAnterior,
        Ultimos3Meses,
        EsteAnio,
        Todo
    };

    public static bool EsValido(string? periodo) =>
        periodo is not null && PeriodosValidos.Contains(periodo);
}
