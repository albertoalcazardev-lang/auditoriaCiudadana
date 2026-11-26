using System.Globalization;
using System.Text.RegularExpressions;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Rutinas compartidas de validación sintáctica y parseo para entradas del registro de solicitudes.
/// </summary>
internal static partial class SolicitudInputParser
{
    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyRegex();

    public static bool EsIdempotencyKeyValida(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return IdempotencyKeyRegex().IsMatch(raw);
    }

    public static bool TryParseFecha(string? raw, out DateOnly fecha)
    {
        fecha = default;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            raw.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fecha);
    }

    public static bool TryParseHora(string? raw, out TimeOnly hora)
    {
        hora = default;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return TimeOnly.TryParseExact(
            raw.Trim(),
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out hora);
    }

    public static bool TryParseAreaReceptoraId(string? raw, out Guid areaId)
    {
        areaId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return Guid.TryParse(raw.Trim(), out areaId) && areaId != Guid.Empty;
    }

    public static bool TryParseEnumName<TEnum>(string? raw, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Any(char.IsDigit) || trimmed[0] is '-' or '+' || trimmed.Contains(','))
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
