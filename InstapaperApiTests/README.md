﻿# OAuth Tests
Tests for the Instapaper Service API Library. These tests validate in two ways:
1. Does the library produce the right output for fixed inputs
2. Can you actually interact with the Instpaper API & does it behave as expected

## API Keys
API Keys are considered secrets, and thus are not included in this source tree.
To be able to successfully run all the tests, you need to obtain API Keys from
the [Instapaper](https://www.instapaper.com/main/request_oauth_consumer_token).

Before putting these keys in the source, it is recommended that you change the
way your git client manages the API Key source file. This is to prevent you from
accidently commiting **your** keys, and sharing them with the world.

Additionally, there are keys that need to be obtained from the API once (ever)
to allow you to run the tests completely. The simplest way to do this is run the
` ` test, and look at it's output in the test log for the information required.

### Prevent sharing of your keys
1. Open a command prompt or terminal
2. `cd` into the directory containing this Readme
3. run `git update-index --skip-worktree InstapaperAPIKey.cs`

### Updating your Consumer Keys & API Information
1. Open `InstapaperAPIKey.cs`
2. Change the `CONSUMER_KEY` & `CONSUMER_KEY_SECRET` values to the appropriate
   values you received from Instapaper. Note, the field names match the
   terminology used commonly in OAuth, and in the Instapaper API Documentation.
3. Update `INSTAPAPER_ACCOUNT` and `INSTAPAPER_PASSWORD` appropriately
3. Save the file!
4. Run `CanGetAccessToken` test, and view the output
5. Copy/paste the information from the output into the appropriate places in
   `InstapaperAPIKey.cs` -- names of the fields and the output should help you
   place these in the locatioj
6. Save the file!
7. Run the `CanVerifyCredentials` test. It will fail the first time, but will
   output your user ID in the failure message. Copy this, and paste it into the
   `INSTAPAPER_USER_ID` field
8. Save the file!

You are now good to go!