using System;

namespace ClassifiedAds.Persistence.PostgreSQL;

public static class NpgsqlTransientHelper
{
    /// <summary>
    /// Detects the Npgsql ManualResetEventSlim ObjectDisposedException
    /// caused by Supabase Supavisor connection resets under Npgsql 10.0.x.
    /// </summary>
    public static bool IsManualResetEventDisposed(Exception exception)
    {
        if (exception is ObjectDisposedException disposedException)
        {
            return string.Equals(
                disposedException.ObjectName,
                "System.Threading.ManualResetEventSlim",
                StringComparison.Ordinal)
                || disposedException.Message.Contains("ManualResetEventSlim");
        }

        return exception.InnerException is not null
            && IsManualResetEventDisposed(exception.InnerException);
    }
}
