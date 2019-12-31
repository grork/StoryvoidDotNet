using System;
using System.Collections.Generic;
using Codevoid.Utilities.OAuth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codevoid.Test.OAuth
{
    [TestClass]
    public class OAuthRequestTests
    {
        private class TestEntropyHelper : IEntropyHelper
        {
            public string nonce = String.Empty;
            public DateTimeOffset timestamp;

            public DateTimeOffset GetDateTime()
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
            return new ClientInformation(clientId: "xvz1evFS4wEEPTGEFPHBog",
                                         clientSecret: "kAcSOqF21Fu85e7zjz7ZN2U4ZRhfV3WpwPAoE3Z7kBw",
                                         token: "370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb",
                                         tokenSecret: "LswwdoUaIvS8ltyTt5jkRh4J50vUPVVHtR2YPi5kE");
        }

        [TestMethod]
        public void CanConstructClientInformation()
        {
            var clientInfo = new ClientInformation("abc", "def");
            Assert.IsNotNull(clientInfo, $"{nameof(ClientInformation)} couldn't be constructed");
            Assert.AreEqual("abc", clientInfo.ClientId, $"{nameof(ClientInformation.ClientId)} didn't match");
            Assert.AreEqual("def", clientInfo.ClientSecret, $"{nameof(ClientInformation.ClientSecret)} didn't match");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowsWhenClientIdMissing()
        {
            _ = new ClientInformation(null!, "def");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowsWhenClientSecretMissing()
        {
            _ = new ClientInformation("abc", null!);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowsWhenConstructingRequestWithoutClientInformation()
        {
            _ = new OAuthRequest(null!, new Uri("https://www.example.com"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowsWhenConstructingRequestWithoutUrl()
        {
            _ = new OAuthRequest(new ClientInformation("abc", "def"), null!);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowsWhenConstructingRequestWithNonHttpsUrl()
        {
            _ = new OAuthRequest(new ClientInformation("abc", "def"), new Uri("http://www.example.com"));
        }

        [TestMethod]
        public void AuthenticationHeaderIsCorrectlyGenerated()
        {
            IEntropyHelper oldEntropyHelper = OAuthRequest.EntropyHelper;
            OAuthRequest.EntropyHelper = new TestEntropyHelper()
            {
                nonce = "kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg",
                timestamp = DateTimeOffset.FromUnixTimeSeconds(1318622958)
            };

            try
            {
                var url = new Uri("https://api.twitter.com/1/statuses/update.json");
                var data = new Dictionary<string, string>
                {
                    { "status", "Hello Ladies + Gentlemen, a signed OAuth request!" },
                    { "include_entities", "true" }
                };

                var request = new OAuthRequest(GetFakeClientInformation(), url);
                request.Data = data;

                var result = request.GenerateAuthHeader();
                Assert.AreEqual("oauth_consumer_key=\"xvz1evFS4wEEPTGEFPHBog\", oauth_nonce=\"kYjzVBB8Y0ZFabxSWbWovY3uYSQ2pTgmZeNu2VS4cg\", oauth_signature=\"tnnArxj06cWHq44gCs1OSKk%2FjLY%3D\", oauth_signature_method=\"HMAC-SHA1\", oauth_timestamp=\"1318622958\", oauth_token=\"370773112-GmHxMAgYyLbNEtIKZeRNFsMKPR9EyMZeS9weJAEb\", oauth_version=\"1.0\"",
                                result,
                                "Authentication headers did not match");
            }
            finally
            {
                OAuthRequest.EntropyHelper = oldEntropyHelper;
            }
        }
    }
}