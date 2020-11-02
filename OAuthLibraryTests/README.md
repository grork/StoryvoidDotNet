# OAuth Tests
Tests for the OAuth Signing Library. These tests validate in two ways:
1. Does the library produce the right output for fixed inputs
2. Can you actually post to twitter

## API Keys
API Keys are considered secrets, and thus are not included in this source tree.
To be able to successfully run all the tests, you need to obtain API Keys from
the [Twitter Developer
Portal](https://developer.twitter.com/en/docs/developer-portal/overview) —
follow their ‘Create app’ flow, to obtain your keys. 

### Updating your Keys
1. Open `TwitterAPIKey.cs` from the root of the repo
2. Change the `PLACEHOLDER` values to the appropriate keys from the Twitter
   Developer Portal. Note, the field names match the terminology used in the
   portals 'Keys and Tokens' section.
3. Save the file into `OAuthLibraryTests\TwitterAPIKey.cs`