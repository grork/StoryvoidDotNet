using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace Codevoid.Utilities.OAuth
{
    /// <summary>
    /// Abstracts away the entropy information used when singing oauth requests
    /// so that it can be fully controlled in tests for reproducability.
    /// </summary>
    internal interface IEntropyProvider
    {
        string GetNonce();
        DateTime GetDateTime();
    }

    /// <summary>
    /// Automatically signs requests for OAuth 1.0a
    /// </summary>
    public sealed class OAuthMessageHandler : DelegatingHandler
    {
        private OAuthSigningHelper signingHelper;

        private OAuthMessageHandler(ClientInformation clientInformation) :
            base(new HttpClientHandler())
        {
            this.signingHelper = new OAuthSigningHelper(clientInformation);
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var signature = await this.signingHelper.GenerateAuthHeaderForHttpRequest(request).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", signature);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an HttpClient instance that will automatically sign requests
        /// using the supplied <see cref="ClientInformation"/>
        /// </summary>
        /// <param name="clientInformation">OAuth token information</param>
        /// <returns>HttpClient that signs requests inline with OAuth 1.0a</returns>
        public static HttpClient CreateOAuthHttpClient(ClientInformation clientInformation)
        {
            var client = new HttpClient(new OAuthMessageHandler(clientInformation));
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(clientInformation.UserAgent);

            return client;
        }
    }

    internal class OAuthSigningHelper
    {
        /// <summary>
        /// Abstract nonce + timestamp info for testing purposes
        /// </summary>
        private class EntropyProviderImpl : IEntropyProvider
        {
            public string GetNonce()
            {
                return Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
            }

            public DateTime GetDateTime()
            {
                return DateTime.Now;
            }
        }

        private static string OperationTypeToString(HttpMethod operation)
        {
            return operation switch
            {
                HttpMethod _ when operation == HttpMethod.Get => "GET",
                _ => "POST",
            };
        }

        private static string GetUrlComponentsForSigning(Uri url)
        {
            return url.GetComponents(
                UriComponents.Scheme
              | UriComponents.UserInfo
              | UriComponents.Host
              | UriComponents.Port
              | UriComponents.Path, UriFormat.SafeUnescaped);
        }

        internal static IEntropyProvider EntropyProvider = new EntropyProviderImpl();

        private readonly ClientInformation clientInfo;
        private readonly HMACSHA1 hmacProvider;

        public OAuthSigningHelper(ClientInformation clientInformation)
        {
            if (clientInformation == null)
            {
                throw new ArgumentNullException(nameof(clientInformation), "Client Information is required");
            }

            this.clientInfo = clientInformation;
            this.hmacProvider = CreateSigningProvider(this.clientInfo);
        }

        private static HMACSHA1 CreateSigningProvider(ClientInformation clientInfo)
        {
            var keyText = $"{clientInfo.ConsumerKeySecret}&{clientInfo.TokenSecret ?? ""}";
            var keyMaterial = Encoding.UTF8.GetBytes(keyText);
            return new HMACSHA1(keyMaterial);
        }

        internal string SignString(string data)
        {
            var signature = this.hmacProvider.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(signature);
        }

        internal async Task<string> GenerateAuthHeaderForHttpRequest(HttpRequestMessage message)
        {
            var timestamp = (new DateTimeOffset(OAuthSigningHelper.EntropyProvider.GetDateTime()).ToUnixTimeSeconds());
            var oauthHeaders = new Dictionary<string, string>
            {
                { "oauth_consumer_key", this.clientInfo.ConsumerKey },
                { "oauth_nonce", OAuthSigningHelper.EntropyProvider.GetNonce() },
                { "oauth_signature_method", "HMAC-SHA1" },
                { "oauth_timestamp", timestamp.ToString() },
                { "oauth_version", "1.0" }
            };

            if (!String.IsNullOrWhiteSpace(this.clientInfo.Token))
            {
                oauthHeaders.Add("oauth_token", this.clientInfo.Token!);
            }

            var merged = new Dictionary<string, string>(oauthHeaders);

            // If it's a post, we need to get the parameters that are in the
            // body if it's form encoded
            if (message.Method == HttpMethod.Post
             && message.Content.Headers.ContentType.MediaType == "application/x-www-form-urlencoded")
            {
                var requestContent = await message.Content.ReadAsStringAsync().ConfigureAwait(false);
                var requestPayload = FormReader.ReadForm(requestContent);
                foreach (var kvp in requestPayload)
                {
                    Debug.Assert(kvp.Value.Count == 1, "Unexpected number of values in the payload");
                    merged.Add(kvp.Key, kvp.Value[0]);
                }
            }
            else
            {
                // Assume it's get, and merge in anything in the query params
                var queryParams = HttpUtility.ParseQueryString(message.RequestUri.Query);
                for (var i = 0; i < queryParams.Count; i++)
                {
                    merged.Add(queryParams.GetKey(i), queryParams.Get(i));
                }
            }

            // Get the signature of the payload + oauth_headers
            var encodedPayloadToSign = OperationTypeToString(message.Method) + "&"
                + Uri.EscapeDataString(GetUrlComponentsForSigning(message.RequestUri)) + "&"
                + Uri.EscapeDataString(ParameterEncoder.FormEncodeValues(merged));
            var signature = this.SignString(encodedPayloadToSign);

            // Now append that signature to our oauth headers, so we can create
            // the full auth header
            oauthHeaders.Add("oauth_signature", signature);

            return ParameterEncoder.FormEncodeValues(oauthHeaders, shouldQuoteValues: true, delimiter: ", ");
        }
    }
}
