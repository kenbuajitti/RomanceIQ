# Hosting RomanceIQ after local testing

Keep StoryIQ's existing Render service intact. Create a separate service for
this prototype, from a separate RomanceIQ repository or a separately configured
service that uses this StoryServer folder.

- Runtime: Node, version 22 or newer.
- Root directory: `StoryServer` if the repository root contains Assets and StoryServer;
  `RomanceIQ/StoryServer` if you committed the ZIP's outer folder too.
- Build command: `npm install` (the service has no external packages).
- Start command: `npm start`.
- Persistent disk mount: `/var/data`; set `DATA_DIR=/var/data/romanceiq`.
- Environment: `NODE_ENV=production`, `OPENAI_API_KEY` as a server secret,
  `OPENAI_MODEL=gpt-4.1-mini` (or a Responses/structured-output compatible
  model your API account can access), `MAX_GENERATIONS_PER_DAY=200`.
- `ALLOWED_ORIGINS`: comma-separated exact HTTPS origins of the game iframe
  and any site that actually hosts the build. For itch.io, inspect the build's
  iframe origin; use that exact origin, with no path or trailing slash.
  The outer itch.io page is not necessarily the iframe origin.
- Render supplies PORT; leave its assigned value intact.
- Use one instance. File persistence is not a multi-instance database.

Check `/health` on the new service. In Unity, open RomanceIQMenu, select the
RomanceIQ object and set Story Server Url to the new service's HTTPS address.
Save the scene. Build via IQ Games > Build RomanceIQ for itch.io, then upload
the new generated build ZIP to a separate RomanceIQ itch.io page.

Never put a key in Unity, WebGL JavaScript, Opening.json or a committed .env.
Do not upload the source ZIP to itch.io as a browser build.

The daily cap counts generation attempts, including failed provider requests;
it is not a dollar budget. A complete new run uses 21 generations (the opening
plus 20 choice responses). CORS restricts browser origins, not arbitrary
clients. This small prototype has no player accounts and is intended first
for controlled testing. Monitor API usage before a broad public launch.
