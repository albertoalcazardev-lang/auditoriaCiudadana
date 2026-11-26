using System.Data.Common;
using System.Net.Sockets;
using MySql.Data.MySqlClient;

namespace VyTSystems.Infrastructure.Repositories;

/// <summary>
/// Inspecciona cadenas de excepciones y AggregateException para clasificar fallos transitorios de base de datos o red.
/// </summary>
internal static class PersistenceExceptionClassifier
{
    public static bool EsFalloDisponibilidadORed(Exception exception)
    {
        foreach (var current in EnumerarExcepciones(exception))
        {
            if (current is DbException or TimeoutException or SocketException or IOException)
            {
                return true;
            }
        }

        return false;
    }

    public static MySqlException? ObtenerMySqlException(Exception exception)
    {
        foreach (var current in EnumerarExcepciones(exception))
        {
            if (current is MySqlException mysqlEx)
            {
                return mysqlEx;
            }
        }

        return null;
    }

    private static IEnumerable<Exception> EnumerarExcepciones(Exception root)
    {
        var visitadas = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var pendientes = new Stack<Exception>();
        pendientes.Push(root);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();
            if (!visitadas.Add(actual))
            {
                continue;
            }

            yield return actual;

            if (actual is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    pendientes.Push(inner);
                }
            }

            if (actual.InnerException is not null)
            {
                pendientes.Push(actual.InnerException);
            }
        }
    }
}
