# SteamApiClient

A Steam client built on [SteamKit2](https://github.com/SteamRE/SteamKit), used to download Steam Workshop items.

## What's Implemented

- Logging in with username and password (Steam Guard supported)
- Listing a user's Workshop items and resolving vanity URLs
- Downloading Workshop files straight from Steam's CDN

The login refresh token and Steam Guard data are saved to `steamConfig.json`, so you only need to sign in once.
