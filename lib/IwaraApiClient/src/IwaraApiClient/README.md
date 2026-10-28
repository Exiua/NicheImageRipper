# IwaraApiClient

A C# client for the [Iwara](https://www.iwara.tv/) API, used by the Iwara parser to rip users' videos and images.

## What's Implemented

- Logging in with username and password
- Looking up a user and paging through their videos and images
- Fetching a single video or image
- Downloading videos (including the `X-Version` signature Iwara requires) and images

Requests go through [ImpersonateClient](../../../ImpersonateClient/src/ImpersonateClient). The ripper uses the login set for
the `iwara` site (**Settings → Sites** in the GUI).
