Storyvoid for Windows
=====================
Before you can build & run this project, you need to set API keys - see below
for details.

## API Keys
API Keys are considered secrets, and thus are not included in this source tree.
To be able to successfully run the application, you need to obtain API Keys
from the [Instapaper](https://www.instapaper.com/main/request_oauth_consumer_token).

### Updating your Consumer Keys & API Information
1. Open `InstapaperAPIKey_app.cs` from the root of the repo
2. Change the `CONSUMER_KEY` & `CONSUMER_KEY_SECRET` values to the appropriate
   values you received from Instapaper. Note, the field names match the
   terminology used commonly in OAuth, and in the Instapaper API Documentation.
3. Save the file to the <solution root>\App folder

You are now good to go!

## Getting to the placeholder page
When in debug mode, it's possible to get to our debug page by pressing `Ctrl +
P`. This **only** works in debug mode.

## Using In-Memory Database
Hold down `Shift` while launching the application to use an in-memory database,
leaving the one on disk intact.

## Resetting the Database
Hold down `Alt` while launching the application to delete any local database
before creating a new, clean database.