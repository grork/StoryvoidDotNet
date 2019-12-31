using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace Codevoid.Utilities.OAuth
{
    internal interface IEntropProvider
    {
        string GetNonce();
        DateTimeOffset GetDateTime();
    }

    public class OAuthSigningHelper
    {
        /// <summary>
        /// Abstract nonce + timestamp info for testing puroses
        /// </summary>
        private class EntropyProviderImpl : IEntropProvider
        {
            public string GetNonce()
            {
                return Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
            }

            public DateTimeOffset GetDateTime()
            {
                return DateTimeOffset.Now;
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

        internal static IEntropProvider EntropyProvider = new EntropyProviderImpl();

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
            var keyText = $"{clientInfo.ClientSecret}&{clientInfo.TokenSecret ?? ""}";
            var keyMaterial = Encoding.UTF8.GetBytes(keyText);
            return new HMACSHA1(keyMaterial);
        }

        private string SignString(string data)
        {
            var signature = this.hmacProvider.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(signature);
        }

        internal async Task<string> GenerateAuthHeaderForHttpRequest(HttpRequestMessage message)
        {
            var oauthHeaders = new Dictionary<string, string>
            {
                { "oauth_consumer_key", this.clientInfo.ClientId },
                { "oauth_nonce", OAuthSigningHelper.EntropyProvider.GetNonce() },
                { "oauth_signature_method", "HMAC-SHA1" },
                { "oauth_timestamp", OAuthSigningHelper.EntropyProvider.GetDateTime().ToUnixTimeSeconds().ToString() },
                { "oauth_version", "1.0" }
            };

            if (!String.IsNullOrWhiteSpace(this.clientInfo.Token))
            {
                oauthHeaders.Add("oauth_token", this.clientInfo.Token!);
            }

            var requestContent = await message.Content.ReadAsStringAsync().ConfigureAwait(false);
            var requestPayload = FormReader.ReadForm(requestContent);

            var merged = new Dictionary<string, string>(oauthHeaders);
            foreach (var kvp in requestPayload)
            {
                Debug.Assert(kvp.Value.Count == 1, "Unexpected number of values in the payload");
                merged.Add(kvp.Key, kvp.Value[0]);
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
