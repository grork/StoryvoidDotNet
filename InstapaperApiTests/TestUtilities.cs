using System;
using Codevoid.Utilities.OAuth;

namespace Codevoid.Test.Instapaper
{
    public static class TestUtilities
    {
        public static ClientInformation GetClientInformation()
        {
            return new ClientInformation(
                InstapaperAPIKey.CLIENT_ID,
                InstapaperAPIKey.CLIENT_SECRET,
                InstapaperAPIKey.ACCESS_TOKEN,
                InstapaperAPIKey.TOKEN_SECRET
            );
        }
    }
}
