using System;
using System.Diagnostics;
using System.Text.Json;

namespace Codevoid.Instapaper
{
    internal static class ExceptionMapper
    {
        private const int UNKNOWN_ERROR = 1250;
        private const int DUPLICATE_FOLDER = 1251;
        private const int BOOKMARK_CONTENTS_UNAVAILABLE = 1550;

        public static InstapaperServiceException FromErrorJson(JsonElement errorElement)
        {
            Debug.Assert(errorElement.ValueKind == JsonValueKind.Object, "Expected extracted error object");

            // Get the error code
            int code = errorElement.GetProperty("error_code").GetInt32();
            switch (code)
            {
                case DUPLICATE_FOLDER:
                    return new DuplicateFolderException();

                case BOOKMARK_CONTENTS_UNAVAILABLE:
                    return new BookmarkContentsUnavailableException();

                case UNKNOWN_ERROR:
                default:
                    var message = errorElement.GetProperty("message").GetString();
                    return new UnknownServiceError(code, message);
            }
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
    public class UnknownServiceError : InstapaperServiceException
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
    public class DuplicateFolderException : InstapaperServiceException
    {
        internal DuplicateFolderException() : base("A folder with this name already exists")
        { }
    }

    /// <summary>
    /// The contents for the requested bookmark are unavailable
    /// </summary>
    public class BookmarkContentsUnavailableException : InstapaperServiceException
    {
        internal BookmarkContentsUnavailableException() : base("Bookmark contents are unavailable")
        { }
    }
}
