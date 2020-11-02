# Instapaper API Tests
Tests for the Instapaper Service API Library. These tests validate in two ways:
1. Does the library produce the right output for fixed inputs
2. Can you actually interact with the Instapaper API & does it behave as
   expected

## API Keys
API Keys are considered secrets, and thus are not included in this source tree.
To be able to successfully run all the tests, you need to obtain API Keys from
the [Instapaper](https://www.instapaper.com/main/request_oauth_consumer_token).

Additionally, there are keys that need to be obtained from the API once (ever)
to allow you to run the tests completely. The simplest way to do this is run the
`CanGetAccessToken` test, and look at its output in the test log for the
information required.

### Updating your Consumer Keys & API Information
1. Open `InstapaperAPIKey.cs` from the root of the repo
2. Change the `CONSUMER_KEY` & `CONSUMER_KEY_SECRET` values to the appropriate
   values you received from Instapaper. Note, the field names match the
   terminology used commonly in OAuth, and in the Instapaper API Documentation.
3. Update `INSTAPAPER_ACCOUNT` and `INSTAPAPER_PASSWORD` appropriately
4. Save the file in `InstapaperApiTests\InstapaperAPIKey.cs`
5. Run `CanGetAccessToken` test, and view the output
6. Copy/paste the information from the output into the appropriate places in
   `InstapaperAPIKey.cs` -- names of the fields and the output should help you
   place these in the correct location
7. Save the file!
8. Run the `CanVerifyCredentials` test. It will fail the first time, but will
   output your user ID in the failure message. Copy this, and paste it into the
   `INSTAPAPER_USER_ID` field
9. Save the file!

You are now good to go!