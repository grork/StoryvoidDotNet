using System;
namespace Codevoid.Instapaper
{
    internal static class Endpoints
    {
        private static readonly Uri baseUri = new Uri("https://www.instapaper.com/api/1/");

        internal static class Access
        {
            internal static readonly Uri AccessToken = new Uri(baseUri, "oauth/access_token");
            internal static readonly Uri VerifyCredentials = new Uri(baseUri, "oauth/verify_credentials");
        }
    }
}