using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Codevoid.Utilities.OAuth
{
    internal interface IEntropyHelper
    {
        string GetNonce();
        DateTimeOffset GetDateTime();
    }

    public class OAuthSigningHelper
    {
        /// <summary>
        /// Abstract nonce + timestamp info for testing puroses
        /// </summary>
        private class EntropyHelperImpl : IEntropyHelper
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

        internal static IEntropyHelper EntropyHelper = new EntropyHelperImpl();

        public FormUrlEncodedContent Data = new FormUrlEncodedContent(null);

        private readonly Uri url;
        private readonly ClientInformation clientInfo;
        private readonly HttpMethod operation;

        public OAuthSigningHelper(ClientInformation clientInformation,
                                  Uri url,
                                  HttpMethod operation)
        {
            if (clientInformation == null)
            {
                throw new ArgumentNullException(nameof(clientInformation), "Client Information is required");
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url), "URL Required");
            }

            if (url.Scheme.ToLowerInvariant() != "https")
            {
                throw new ArgumentException("Only HTTPS urls are supported", nameof(url));
            }

            this.clientInfo = clientInformation;
            this.url = url;
            this.operation = operation;
        }

        private string SignString(string data)
        {
            var keyText = $"{this.clientInfo.ClientSecret}&{this.clientInfo.TokenSecret ?? ""}";
            var keyMaterial = Encoding.UTF8.GetBytes(keyText);
            var hmacProvider = new HMACSHA1(keyMaterial);
            var signature = hmacProvider.ComputeHash(Encoding.UTF8.GetBytes(data));

            return Convert.ToBase64String(signature);
        }

        internal string GenerateAuthHeader()
        {
            var oauthHeaders = new Dictionary<string, string>
            {
                { "oauth_consumer_key", this.clientInfo.ClientId },
                { "oauth_nonce", OAuthSigningHelper.EntropyHelper.GetNonce() },
                { "oauth_signature_method", "HMAC-SHA1" },
                { "oauth_timestamp", OAuthSigningHelper.EntropyHelper.GetDateTime().ToUnixTimeSeconds().ToString() },
                { "oauth_version", "1.0" }
            };

            if (!String.IsNullOrWhiteSpace(this.clientInfo.Token))
            {
                oauthHeaders.Add("oauth_token", this.clientInfo.Token!);
            }

            // Because the signature is over the data AND oauth headers, we need
            // to merge them all into one giant dictionary before we encode it
            var merged = new Dictionary<string, string>(oauthHeaders);
            if (this.Data != null)
            {
                foreach (var kvp in this.Data)
                {
                    merged.Add(kvp.Key, kvp.Value);
                }
            }

            // Get the signature of the payload + oauth_headers
            var encodedPayloadToSign = OperationTypeToString(this.operation) + "&"
                + Uri.EscapeDataString(this.url.ToString()) + "&"
                + Uri.EscapeDataString(ParameterEncoder.FormEncodeValues(merged));
            var signature = this.SignString(encodedPayloadToSign);

            // Now append that signature to our oauth headers, so we can create
            // the full auth header
            oauthHeaders.Add("oauth_signature", signature);

            return ParameterEncoder.FormEncodeValues(oauthHeaders, shouldQuoteValues: true, delimiter: ", ");
        }
    }
}
