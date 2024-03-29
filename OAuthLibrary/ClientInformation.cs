﻿using System.Net.Http.Headers;

namespace Codevoid.Utilities.OAuth;

/// <summary>
/// Holds the OAuth token (both client, and authenticated tokens)
/// for use with <see cref="OAuthMessageHandler"/> to automatically sign
/// requests.
/// </summary>
public sealed class ClientInformation
{
    private string productName = "Codevoid+OAuth+Library";
    private string productVersion = "1.0";
    private Lazy<ProductInfoHeaderValue>? userAgentValue;

    public readonly string ConsumerKey;
    public readonly string ConsumerKeySecret;
    public readonly string? Token;
    public readonly string? TokenSecret;

    /// <summary>
    /// Constructs this class with the supplied client information &amp;
    /// optional authentication tokens for use with <see cref="OAuthMessageHandler"/>
    /// </summary>
    /// <param name="consumerKey">Client Identifier (aka identify your app)</param>
    /// <param name="consumerKeySecret">Client signing secret (aka unique to your app)</param>
    /// <param name="token">Authentication token (identifying the user)</param>
    /// <param name="tokenSecret">Authentication secret for signing requests</param>
    public ClientInformation(string consumerKey,
                             string consumerKeySecret,
                             string? token = null,
                             string? tokenSecret = null)
    {
        if (String.IsNullOrWhiteSpace(consumerKey))
        {
            throw new ArgumentNullException(nameof(consumerKey),
                                           "Consumer Key is required");
        }

        if (String.IsNullOrWhiteSpace(consumerKeySecret))
        {
            throw new ArgumentNullException(nameof(consumerKeySecret),
                                            "Consumer Key Secret is required");
        }

        this.ConsumerKey = consumerKey;
        this.ConsumerKeySecret = consumerKeySecret;
        this.Token = token;
        this.TokenSecret = tokenSecret;

        this.ResetUserAgentValue();
    }

    private void ResetUserAgentValue()
    {
        if (this.userAgentValue is not null && !this.userAgentValue.IsValueCreated)
        {
            // No need to reset the lazy value if we've not yet evaluated the
            // the user agent value.
            return;
        }

        this.userAgentValue = new Lazy<ProductInfoHeaderValue>(() =>
            new ProductInfoHeaderValue(this.productName, this.productVersion));
    }

    /// <summary>
    /// A friendly name that is included in the User-Agent header
    /// </summary>
    public string ProductName
    {
        get { return this.productName; }
        set
        {
            this.productName = value;
            this.ResetUserAgentValue();
        }
    }

    /// <summary>
    /// A representative version included in the User-Agent header
    /// </summary>
    public string ProductVersion
    {
        get { return this.productVersion; }
        set
        {
            this.productVersion = value;
            this.ResetUserAgentValue();
        }
    }

    /// <summary>
    /// User-Agent header instance to be used with HttpClient
    /// </summary>
    public ProductInfoHeaderValue UserAgent => this.userAgentValue!.Value;
}