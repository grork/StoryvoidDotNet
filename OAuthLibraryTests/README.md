# OAuth Tests
Tests for the OAuth Signing Library. These tests validate the signing by verifying
the library produce the right output for fixed inputs.

Note, it *used* to verify by posting to twitter, but Twitter (now X) removed it's
free API tier, so the tests now only verify the signing process by leveraging
known inputs & ouputs.