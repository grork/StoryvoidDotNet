using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Codevoid.Utilities.OAuth;
using Xunit;

namespace Codevoid.Test.OAuth
{
    public class OAuthSigningHelperTests
    {
        private static void ThrowIfValueIsAPIKeyHasntBeenSet(string valueToTest, string valueName)
        {
            if (!String.IsNullOrWhiteSpace(valueToTest) && (valueToTest != "PLACEHOLDER"))
            {
                return;
            }

            throw new ArgumentOutOfRangeException(valueName, "You must replace the placeholder value. See README.md");
        }

        private class TestEntropyProvider : IEntropyProvider
        {
            public string nonce = String.Empty;
            public DateTime timestamp;

            public DateTime GetDateTime()
            {
                return this.timestamp;
            }

            public string GetNonce()
            {
                return this.nonce;
            }
        }

        private static ClientInformation GetFakeClientInformation()
        {
            // These values are from twitters example tutorial, so are intentionally
            // invalid, and well known.
            return new ClientInformation(consumerKey: "xvz1evFS4wEEPTGEFPHBog",
                                         consumerKeySecret: "kAcSOqF21Fu85e7zjz7ZN2U4ZRhfV3WpwPAoE3Z7kBw",
                                         token: "370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb",
                                         tokenSecret: "LswwdoUaIvS8ltyTt5jkRh4J50vUPVVHtR2YPi5kE");
        }

        private static ClientInformation GetRealClientInformation()
        {
            ThrowIfValueIsAPIKeyHasntBeenSet(TwitterAPIKey.API_KEY, nameof(TwitterAPIKey.API_KEY));
            ThrowIfValueIsAPIKeyHasntBeenSet(TwitterAPIKey.API_SECRET_KEY, nameof(TwitterAPIKey.API_SECRET_KEY));
            ThrowIfValueIsAPIKeyHasntBeenSet(TwitterAPIKey.ACCESS_TOKEN, nameof(TwitterAPIKey.ACCESS_TOKEN));
            ThrowIfValueIsAPIKeyHasntBeenSet(TwitterAPIKey.ACCESS_TOKEN_SECRET, nameof(TwitterAPIKey.ACCESS_TOKEN_SECRET));

            return new ClientInformation(consumerKey: TwitterAPIKey.API_KEY,
                                         consumerKeySecret: TwitterAPIKey.API_SECRET_KEY,
                                         token: TwitterAPIKey.ACCESS_TOKEN,
                                         tokenSecret: TwitterAPIKey.ACCESS_TOKEN_SECRET);
        }

        private static HttpRequestMessage GetPostRequestForData(IDictionary<string, string> data, Uri url)
        {
            var content = new FormUrlEncodedContent(data);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            return request;
        }

        private static Uri GetUriWithDataAsQueryParams(IDictionary<string, string> data, Uri baseUri)
        {
            var builder = new UriBuilder(baseUri);
            var queryParams = HttpUtility.ParseQueryString(builder.Query);
            foreach (var kvp in data)
            {
                queryParams.Add(kvp.Key, kvp.Value);
            }

            builder.Query = queryParams.ToString();

            return builder.Uri;
        }

        private static HttpRequestMessage GetGetRequestForData(IDictionary<string, string> data, Uri baseUri)
        {
            return new HttpRequestMessage(HttpMethod.Get, GetUriWithDataAsQueryParams(data, baseUri));
        }

        private static IEntropyProvider SetEntropyHelper(string nonce, long unixTimeInSeconds)
        {
            var oldEntropyHelper = OAuthSigningHelper.EntropyProvider;
            OAuthSigningHelper.EntropyProvider = new TestEntropyProvider()
            {
                nonce = nonce,
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimeInSeconds).LocalDateTime
            };
            return oldEntropyHelper;
        }

        [Fact]
        public void CanConstructClientInformation()
        {
            var clientInfo = new ClientInformation("abc", "def");
            Assert.NotNull(clientInfo);
            Assert.Equal("abc", clientInfo.ConsumerKey);
            Assert.Equal("def", clientInfo.ConsumerKeySecret);
        }

        [Fact]
        public void ThrowsWhenConsumerKeyMissing()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ClientInformation(null!, "def"));
        }

        [Fact]
        public void ThrowsWhenConsumerKeySecretMissing()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ClientInformation("abc", null!));
        }

        [Fact]
        public void ThrowsWhenConstructingRequestWithoutClientInformation()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new OAuthSigningHelper(null!));
        }

        [Fact]
        public void SignatureGeneratedCorrectly()
        {
            IEntropyProvider oldEntropyHelper = SetEntropyHelper(
                nonce: "kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg",
                unixTimeInSeconds: 1318622958
            );

            try
            {
                var url = new Uri("https://api.twitter.com/1/statuses/update.json");
                var data = new Dictionary<string, string>
                {
                    { "status", "Hello Ladies + Gentlemen, a signed OAuth request!" },
                    { "include_entities", "true" }
                };

                var signingHelper = new OAuthSigningHelper(GetFakeClientInformation());
                var signature = signingHelper.SignString("POST&https%3A%2F%2Fapi.twitter.com%2F1%2Fstatuses%2Fupdate.json&include_entities%3Dtrue%26oauth_consumer_key%3Dxvz1evFS4wEEPTGEFPHBog%26oauth_nonce%3DkYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg%26oauth_signature_method%3DHMAC-SHA1%26oauth_timestamp%3D1318622958%26oauth_token%3D370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb%26oauth_version%3D1.0%26status%3DHello%2520Ladies%2520%252B%2520Gentlemen%252C%2520a%2520signed%2520OAuth%2520request%2521");
                Assert.Equal("tnnArxj06cWHq44gCs1OSKk/jLY=", signature); // Signature wasn't generated properly"
            }
            finally
            {
                OAuthSigningHelper.EntropyProvider = oldEntropyHelper;
            }
        }

        [Fact]
        public async Task AuthenticationHeaderIsCorrectlyGeneratedForPostMethod()
        {
            IEntropyProvider oldEntropyHelper = SetEntropyHelper(
                nonce: "kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg",
                unixTimeInSeconds: 1318622958
            );

            try
            {
                var url = new Uri("https://api.twitter.com/1/statuses/update.json");
                var data = new Dictionary<string, string>
                {
                    { "status", "Hello Ladies + Gentlemen, a signed OAuth request!" },
                    { "include_entities", "true" }
                };

                var signingHelper = new OAuthSigningHelper(GetFakeClientInformation());
                var result = await signingHelper.GenerateAuthHeaderForHttpRequest(GetPostRequestForData(data, url));
                Assert.Equal("oauth_consumer_key=\"xvz1evFS4wEEPTGEFPHBog\", oauth_nonce=\"kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg\", oauth_signature=\"tnnArxj06cWHq44gCs1OSKk%2FjLY%3D\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"1318622958\", oauth_token=\"370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb\", oauth_version=\"1.0\"",
                             result); // Authentication headers did not match
            }
            finally
            {
                OAuthSigningHelper.EntropyProvider = oldEntropyHelper;
            }
        }

        [Fact]
        public async Task AuthenticationHeaderIsCorrectlyGeneratedForGetMethod()
        {
            IEntropyProvider oldEntropyHelper = SetEntropyHelper(
                nonce: "kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg",
                unixTimeInSeconds: 1318622958
            );

            try
            {
                var url = new Uri("https://api.twitter.com/1/statuses/update.json");
                var data = new Dictionary<string, string>
                {
                    { "status", "Hello Ladies + Gentlemen, a signed OAuth request!" },
                    { "include_entities", "true" }
                };

                var signingHelper = new OAuthSigningHelper(GetFakeClientInformation());
                var result = await signingHelper.GenerateAuthHeaderForHttpRequest(GetGetRequestForData(data, url));
                Assert.Equal("oauth_consumer_key=\"xvz1evFS4wEEPTGEFPHBog\", oauth_nonce=\"kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg\", oauth_signature=\"OgeXpQpLHCLpVVnrQjAwHmPrU7c%3D\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"1318622958\", oauth_token=\"370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb\", oauth_version=\"1.0\"",
                             result); // Authentication headers did not match
            }
            finally
            {
                OAuthSigningHelper.EntropyProvider = oldEntropyHelper;
            }
        }

        [Fact]
        public async Task XAuthAuthenticationHeaderCorrectlyGenerated()
        {
            IEntropyProvider oldEntropyHelper = SetEntropyHelper(
                nonce: "6AN2dKRzxyGhmIXUKSmp1JcB4pckM8rD3frKMTmVAo",
                unixTimeInSeconds: 1284565601
            );

            try
            {
                var url = new Uri("https://api.twitter.com/oauth/access_token");
                var data = new Dictionary<string, string>
                {
                    { "x_auth_username", "oauth_test_exec" },
                    { "x_auth_password", "twitter-xauth" },
                    { "x_auth_mode", "client_auth" }
                };

                var signingHelper = new OAuthSigningHelper(new ClientInformation("JvyS7DO2qd6NNTsXJ4E7zA", "9z6157pUbOBqtbm0A0q4r29Y2EYzIHlUwbF4Cl9c"));
                var result = await signingHelper.GenerateAuthHeaderForHttpRequest(GetPostRequestForData(data, url));
                Assert.Equal("oauth_consumer_key=\"JvyS7DO2qd6NNTsXJ4E7zA\", oauth_nonce=\"6AN2dKRzxyGhmIXUKSmp1JcB4pckM8rD3frKMTmVAo\", oauth_signature=\"1L1oXQmawZAkQ47FHLwcOV%2Bkjwc%3D\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"1284565601\", oauth_version=\"1.0\"",
                                result); // Authentication headers did not match
            }
            finally
            {
                OAuthSigningHelper.EntropyProvider = oldEntropyHelper;
            }
        }

        [Fact]
        public async Task CanVerifyTwitterCredentials()
        {
            using var client = OAuthMessageHandler.CreateOAuthHttpClient(GetRealClientInformation());
            var url = new Uri("https://api.twitter.com/1.1/account/verify_credentials.json");
            var body = await client.GetStringAsync(url);
            var responsePayload = JsonDocument.Parse(body);
            Assert.True(responsePayload.RootElement.TryGetProperty("screen_name", out var value)); // screen_name field was missing
            Assert.Equal("CodevoidTest", value.ToString()); // Wrong screen name returned
        }

        [Fact]
        public async Task CanPostStatusToTwitter()
        {
            using var client = OAuthMessageHandler.CreateOAuthHttpClient(GetRealClientInformation());
            var url = new Uri("https://api.twitter.com/1.1/statuses/update.json");
            var data = new Dictionary<string, string> { { "status", $"Test@Status % 78 update: {DateTimeOffset.Now.ToUnixTimeSeconds().ToString()}" } };
            var response = await client.PostAsync(url, new FormUrlEncodedContent(data));
            var rawResponse = await response.Content.ReadAsStringAsync();
            var responsePayload = JsonDocument.Parse(rawResponse);

            // Get the response out of the nested payload
            Assert.True(responsePayload.RootElement.TryGetProperty("text", out var textField)); // Text field was missing
            Assert.Equal(data["status"], textField.ToString()); // Wrong Status
        }

        [Fact]
        public async Task CanMakeGetRequestWithPayload()
        {
            using var client = OAuthMessageHandler.CreateOAuthHttpClient(GetRealClientInformation());
            var url = new Uri("https://api.twitter.com/1.1/statuses/home_timeline.json");
            var data = new Dictionary<string, string> { { "count", "1" } };
            var body = await client.GetStringAsync(GetUriWithDataAsQueryParams(data, url));
            var responsePayload = JsonDocument.Parse(body);

            // Get the response out of the nested payload
            Assert.Equal(JsonValueKind.Array, responsePayload.RootElement.ValueKind); // Root response was not an array
            Assert.Equal(1, responsePayload.RootElement.GetArrayLength()); // Wrong Number of elements returned
        }

    }
}