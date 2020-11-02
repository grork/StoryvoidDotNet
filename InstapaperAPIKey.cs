namespace Codevoid.Test.Instapaper
{
    /// <summary>
    /// Holds the API Key for Instapaper so that you can run tests that actually
    /// do real work. Note, you need to get your own key, and place it in the
    /// appropriate constants below.
    /// </summary>
    public static class InstapaperAPIKey
    {
        // Instapaper Tokens. Replace with tokens & IDs
        // that are issued to you for Instapapers developer
        // access: https://www.instapaper.com/main/request_oauth_consumer_token
        public const string CONSUMER_KEY = "PLACEHOLDER";
        public const string CONSUMER_KEY_SECRET = "PLACEHOLDER";

        // Instapaper Account credentials for the account used to run the tests
        // against.
        public const string INSTAPAPER_ACCOUNT = "PLACEHOLDER";
        public const string INSTAPAPER_PASSWORD = "PLACEHOLDER";

        // These are for a specific account login instance, and
        // need to be captured by following the OAuth flow. The
        // easiest way is to run CanGetAccessToken in InstapaperApiTests's
        // Account.Tests.cs and look at the results to capture specific values.
        public const string ACCESS_TOKEN = "PLACEHOLDER";
        public const string TOKEN_SECRET = "PLACEHOLDER";
        public const ulong INSTAPAPER_USER_ID = -1; // Placeholder
    }
}
