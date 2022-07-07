using System.Diagnostics;
using System.Text.Json;

namespace Codevoid.Instapaper;

internal static class ExceptionMapper
{
    private const int RATE_LIMIT_EXCEEDED = 1040;
    private const int PREMIUM_ACCOUNT_REQUIRED = 1041;
    private const int APPLICATION_SUSPENDED = 1042;
    private const int DOMAIN_REQUIRES_FULL_CONTENT = 1220;
    private const int DOMAIN_BLOCKS_INSTAPAPER = 1221;
    private const int INVALID_URL_SPECIFIED = 1240;
    private const int INVALID_OR_MISSING_ENTITY_ID = 1241;
    private const int INVALID_OR_MISSING_FOLDER_ID = 1242;
    private const int INVALID_OR_MISSING_PROGRESS = 1243;
    private const int INVALID_OR_MISSING_PROGRESS_TIMESTAMP = 1244;
    private const int PRIVATE_BOOKMARKS_REQUIRE_SUPPLIED_CONTENT = 1245;
    private const int UNKNOWN_ERROR = 1250;
    private const int DUPLICATE_FOLDER = 1251;
    private const int CANNOT_ADD_BOOKMARKS_TO_FOLDER = 1252;
    private const int UNEXPECTED_SERVICE_ERROR = 1500;
    private const int BOOKMARK_CONTENTS_UNAVAILABLE = 1550;

    public static InstapaperServiceException FromErrorJson(JsonElement errorElement)
    {
        Debug.Assert(errorElement.ValueKind == JsonValueKind.Object, "Expected extracted error object");

        InstapaperServiceException UnknownError(int unknownCode, JsonElement errorDetails)
        {
            var message = errorDetails.GetProperty("message").GetString()!;
            return new UnknownServiceError(unknownCode, message);
        }

        // Get the error code
        int code = errorElement.GetProperty("error_code").GetInt32();
        return code switch
        {
            DUPLICATE_FOLDER => new DuplicateFolderException(),
            BOOKMARK_CONTENTS_UNAVAILABLE => new BookmarkContentsUnavailableException(),
            INVALID_OR_MISSING_ENTITY_ID => new EntityNotFoundException(),
            _ => UnknownError(code, errorElement),
        };
    }
}

public abstract class InstapaperServiceException : Exception
{
    public InstapaperServiceException(string message) : base(message)
    { }
}

/// <summary>
/// We encountered a service error, and but we don't know exactly what it is.
/// The <see cref="ErrorCode"/> field provides more information, and the
/// service message is in the <see cref="Message"/> property.
/// </summary>
public sealed class UnknownServiceError : InstapaperServiceException
{
    /// <summary>
    /// Instapaper error code returned by the service
    /// </summary>
    public readonly int ErrorCode;

    internal UnknownServiceError(int errorCode, string message = "An unknown error occured on the service") : base(message)
    {
        this.ErrorCode = errorCode;
    }
}

/// <summary>
/// A folder with the same name already exists on the service
/// </summary>
public sealed class DuplicateFolderException : InstapaperServiceException
{
    internal DuplicateFolderException() : base("A folder with this name already exists")
    { }
}

/// <summary>
/// The contents for the requested bookmark are unavailable
/// </summary>
public sealed class BookmarkContentsUnavailableException : InstapaperServiceException
{
    internal BookmarkContentsUnavailableException() : base("Bookmark contents are unavailable")
    { }
}

/// <summary>
/// Bookmark or folder being operated on wasn't found on the service
/// </summary>
public sealed class EntityNotFoundException : InstapaperServiceException
{
    internal EntityNotFoundException() : base("Entity with that ID was not found")
    { }
}