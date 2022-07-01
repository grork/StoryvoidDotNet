using System.Data;

namespace Codevoid.Storyvoid;

/// <summary>
/// Sqlite error codes aren't in the Microsoft.Data.Sqlite namespace, so we
/// need our own for semantic usages rather than random magic values
/// </summary>
internal static class SqliteErrorCodes
{
    internal const int SQLITE_CONSTRAINT = 19;
    internal const int SQLITE_CONSTRAINT_FOREIGNKEY = 787;
    internal const int SQLITE_CONSTRAINT_PRIMARYKEY = 1555;
    internal const int SQLITE_CONSTRAINT_UNIQUE = 2067;
}

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

    /// <summary>
    /// Get a string from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static string GetString(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.GetString(columnIndex);
    }

    /// <summary>
    /// Get a Long from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static long GetInt64(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.GetInt64(columnIndex);
    }

    /// <summary>
    /// Get a Uri from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static Uri GetUri(this IDataReader instance, string name)
    {
        var uriString = instance.GetString(name);
        return new Uri(uriString);
    }

    /// <summary>
    /// Get a Uri from a row, using it's name rather than ordinal, or a null
    /// if there is no URL value
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static Uri? GetNullableUri(this IDataReader instance, string name)
    {
        if (instance.IsDBNull(name))
        {
            return null;
        }

        var value = instance.GetString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return GetUri(instance, name);
    }

    /// <summary>
    /// Get a Float from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static float GetFloat(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.GetFloat(columnIndex);
    }

    /// <summary>
    /// Get a DateTime from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static DateTime GetDateTime(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.GetDateTime(columnIndex);
    }

    /// <summary>
    /// Get a Boolean from a row, using it's name rather than ordinal
    /// </summary>
    /// <param name="name">Column to return</param>
    /// <returns>Value from that column</returns>
    public static bool GetBoolean(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.GetBoolean(columnIndex);
    }

    /// <summary>
    /// Is a particular column in this row null, by name
    /// </summary>
    /// <param name="name">Column Name to check</param>
    /// <returns>True if null, false otherwise</returns>
    public static bool IsDBNull(this IDataReader instance, string name)
    {
        var columnIndex = instance.GetOrdinal(name);
        return instance.IsDBNull(columnIndex);
    }

    /// <summary>
    /// Checks if the reader has an actual named column.
    /// 
    /// Note, this says nothing about the *value* of the column -- it could be
    /// null.
    /// </summary>
    /// <param name="name">Column name to check for</param>
    /// <returns>True if the column is in the row, otherwise false.</returns>
    public static bool HasColumn(this IDataReader instance, string name)
    {
        for (var index = 0; index < instance.FieldCount; index += 1)
        {
            if (instance.GetName(index) == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Add a named parameter of type long to this command.
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, long value)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = DbType.Int64;
        parameter.ParameterName = name;
        parameter.Value = value;

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Add a named parameter of type string to this command.
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, string value)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = DbType.String;
        parameter.ParameterName = name;
        parameter.Value = value;

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Add a named parameter of type float to this command.
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, float value)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = DbType.Single;
        parameter.ParameterName = name;
        parameter.Value = value;

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Add a named parameter of type Uri to this command. Note, that the
    /// URL will be turned into a string for storage
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, Uri? value)
    {
        if (value is null)
        {
            instance.AddNull(name, DbType.String);
            return;
        }

        var parameter = instance.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Add a named parameter of type DateTime to this command.
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, DateTime value)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = DbType.DateTime;
        parameter.ParameterName = name;
        parameter.Value = value;

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Add a named parameter of type bool to this command.
    /// </summary>
    /// <param name="name">Name of the parameter in the query</param>
    /// <param name="value">Value of the parameter</param>
    public static void AddParameter(this IDbCommand instance, string name, bool value)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = DbType.Boolean;
        parameter.ParameterName = name;
        parameter.Value = value;

        instance.Parameters.Add(parameter);
    }

    /// <summary>
    /// Adds a null into the command for the given param & type
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <param name="type">The datatype</param>
    public static void AddNull(this IDbCommand instance, string name, DbType type)
    {
        var parameter = instance.CreateParameter();
        parameter.DbType = type;
        parameter.ParameterName = name;
        parameter.Value = DBNull.Value;

        instance.Parameters.Add(parameter);
    }

    public static IDbTransaction? BeginTransactionIfNeeded(this IDbCommand instance)
    {
        var t = (instance.Transaction != null) ? null : instance.Connection.BeginTransaction();
        if (t is not null)
        {
            instance.Transaction = t;
        }

        return t;
    }
}