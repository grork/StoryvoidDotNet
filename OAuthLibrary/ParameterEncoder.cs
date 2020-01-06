using System;
using System.Collections.Generic;
using System.Text;

namespace Codevoid.Utilities.OAuth
{
    /// <summary>
    /// Helper class for performing appropriate encoding of values to be included
    /// in OAuth header
    /// </summary>
    internal static class ParameterEncoder
    {
        internal static string FormEncodeValues(IDictionary<string, string> valuesToEncode, string delimiter = "&", bool shouldQuoteValues = false)
        {
            IDictionary<string, string> encodedValues =
                new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var keyAndValueToEncode in valuesToEncode)
            {
                var encodedKey = Uri.EscapeDataString(keyAndValueToEncode.Key);
                var encodedValue = Uri.EscapeDataString(keyAndValueToEncode.Value);
                encodedValues.Add(encodedKey, encodedValue);
            }

            var sb = new StringBuilder();
            var quoteChar = (shouldQuoteValues ? "\"" : "");

            foreach (var encodedKeyAndValue in encodedValues)
            {
                sb.Append($"{encodedKeyAndValue.Key}={quoteChar}{encodedKeyAndValue.Value}{quoteChar}{delimiter}");
            }

            // Remove the trailing & we added
            sb.Length -= delimiter.Length;

            return sb.ToString();
        }
    }
}