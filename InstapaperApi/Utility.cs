using System.Net;

namespace Codevoid.Instapaper;

internal static class Helpers
{
    /// <summary>
    /// Instapaper service returns structured errors in 400 (bad request)
    /// status codes, but not others. This hides those details from consumers
    /// of the raw http requests
    /// </summary>
    /// <param name="statusCode">Status to inspect</param>
    /// <returns>
    /// True, if this code is fatal (e.g. don't parse the body)
    /// </returns>
    internal static bool IsFatalStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => false,
        _ => true,
    };
}