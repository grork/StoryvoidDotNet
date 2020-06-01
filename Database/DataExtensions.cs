using System;
using System.Data;

namespace Codevoid.Storyvoid
{
    /// <summary>
    /// Simple extensions that make working with the ADO.NET data objects simpler
    /// </summary>
    internal static class DataExtensions
    {
        /// <summary>
        /// Create a command with the specified query text.
        /// </summary>
        /// <param name="queryText">Query text to set on this command</param>
        /// <returns>Command instance</returns>
        internal static IDbCommand CreateCommand(this IDbConnection instance, string queryText)
        {
            var command = instance.CreateCommand();
            command.CommandText = queryText;

            return command;
        }
    }
}
