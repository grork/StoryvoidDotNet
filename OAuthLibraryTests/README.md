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

Before putting these keys in the source, it is recommended that you change the
way your git client manages the API Key source file. This is to prevent you from
accidentally committing **your** keys, and sharing them with the world.

### Prevent sharing of your keys
1. Open a command prompt or terminal
2. `cd` into the directory containing this Readme
3. run `git update-index --skip-worktree TwitterAPIKey.cs`

### Updating your Keys
1. Open `TwitterAPIKey.cs`
2. Change the `PLACEHOLDER` values to the appropriate keys from the Twitter
   Developer Portal. Note, the field names match the terminology used in the
   portals 'Keys and Tokens' section.
3. Save the file!