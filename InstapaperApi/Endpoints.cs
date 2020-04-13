using System;

namespace Codevoid.Instapaper
{
    internal static class Endpoints
    {
        private static readonly Uri baseUri = new Uri("https://www.instapaper.com/api/1/");

        internal static class Access
        {
            internal static readonly Uri AccessToken = new Uri(baseUri, "oauth/access_token");
            internal static readonly Uri VerifyCredentials = new Uri(baseUri, "account/verify_credentials");
        }

        internal static class Bookmarks
        {
            internal static readonly Uri List = new Uri(baseUri, "bookmarks/list");
            internal static readonly Uri Add = new Uri(baseUri, "bookmarks/add");
            internal static readonly Uri Delete = new Uri(baseUri, "bookmarks/delete");
            internal static readonly Uri Move = new Uri(baseUri, "bookmarks/move");
            internal static readonly Uri UpdateReadProgress = new Uri(baseUri, "bookmarks/update_read_progress");
            internal static readonly Uri Star = new Uri(baseUri, "bookmarks/star");
            internal static readonly Uri Unstar = new Uri(baseUri, "bookmarks/unstar");
            internal static readonly Uri Archive = new Uri(baseUri, "bookmarks/archive");
            internal static readonly Uri Unarchive = new Uri(baseUri, "bookmarks/unarchive");
            internal static readonly Uri GetText = new Uri(baseUri, "bookmarks/get_text");
        }

        internal static class Folders
        {
            internal static readonly Uri List = new Uri(baseUri, "folders/list");
            internal static readonly Uri Add = new Uri(baseUri, "folders/add");
            internal static readonly Uri Delete = new Uri(baseUri, "folders/delete");
        }
    }
}