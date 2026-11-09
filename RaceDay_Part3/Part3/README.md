# RaceDay — Part 3: MVC Web Application, Azure Blob Storage and Docker

RaceDay is an event management platform for South African road running, walking and cycling events.
Part 3 adds a polished **ASP.NET Core MVC web application** that consumes the REST API, **Azure Blob Storage**
(inside the API) for event banners and profile pictures, and a **Docker** container for the web app.

```
 Browser  ──►  RaceDay.Web (MVC, in Docker)  ──HTTP──►  RaceDay.Api (REST)  ──►  SQL Server
                 session + role + pages                  business rules           (EF Core)
                                                              │
                                                              └──►  Azure Blob Storage (images)
```

- **The MVC app has no database access and no business logic.** Every read and write is an HTTP call to the API.
- **Only the API talks to Azure.** The web app uploads and downloads images through API endpoints.

## The two roles

| Role | Navigation and pages |
|---|---|
| **Organiser** | **Dashboard** (events created, upcoming events, total enrolments, enrolments per event, next event date) · **My events** (create / edit / delete, banner upload) · **Categories** (age or distance) · **Routes** · **Enrolments** per event (participant, category, status) · **Results entry** (finish time and position) · **Profile** |
| **Participant** | **Browse events** (upcoming, with filters for location, type and dates) · **Event page** (details, categories, route, weather, banner) · **Enrol** by choosing a category · **My enrolments** (colour-coded status) · **My results** (race history, e.g. "47th out of 312 finishers") · **Profile** (update details, upload a profile picture) |

Visitors who are not logged in can browse events but must log in to enrol. Each role sees a different navigation menu,
and protected pages redirect to the login page (wrong role → an "access denied" page).

## How it works

- **Session authentication.** Logging in through the MVC app calls `POST /api/auth/login`. The API's session cookie is kept
  *inside the web app's server-side session* (never exposed to the browser's JavaScript) and sent with every later API call, so the API
  still decides who the user is and what they may do. The signed-in user and role are kept in the web session and drive the navigation
  and the `[RoleAuthorize]` filter. Logging out ends both sessions. If the API session expires the user is sent back to the login page.
- **Friendly errors.** Invalid input shows field-level messages (client- and server-side). API errors become one readable sentence.
  If the API is down the pages show "The RaceDay service is not responding right now" instead of crashing. Unhandled errors show a
  friendly error page — never a stack trace.
- **UI.** Bootstrap 5 with a RaceDay theme (navy, orange and lime; Barlow Condensed headings), one stylesheet used on every page, and
  enrolment statuses colour coded: **green = Confirmed, amber = Pending, grey = Cancelled**.

## Azure Blob Storage integration

- The API (`Services/BlobStorageService.cs`) uploads images to a **private** blob container (`raceday-images`).
- Uploads are validated by the file's real signature (JPG, PNG or WebP, max 5 MB), so a renamed text file is rejected.
- The database stores only the **blob name** (`Events.BannerImageBlob`, `Users.ProfilePictureBlob`).
- Images are served `Azure → API → MVC → browser`: the API downloads the blob, the MVC `/media/...` route streams it to the browser.
- If storage is unavailable the API answers **502** and the page shows a friendly message.

### Set up storage (pick one)

**Option A — a real Azure Storage account (recommended; you can show the blobs in the Azure portal)**
1. In the [Azure portal](https://portal.azure.com) create a **Storage account** (Standard, LRS).
2. Open it → **Security + networking → Access keys** → copy the **Connection string** of *key1*.
3. Store it as a *user secret* so it never goes to GitHub (from `Part3/RaceDay.Api`):
   ```bash
   dotnet user-secrets set "AzureBlob:ConnectionString" "<paste the connection string>"
   ```

**Option B — the free Azurite emulator (no Azure account needed)**
```bash
docker run -d --name azurite -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
```
The default `appsettings.json` already uses `UseDevelopmentStorage=true`, which points at Azurite.

## Run it

**Prerequisites:** .NET 8 SDK, SQL Server (Express/LocalDB), Docker Desktop, and Azure storage (above).

1. **Database.** Set your server in `Part3/RaceDay.Api/appsettings.json` (`ConnectionStrings:RaceDayDb`; the database is `RaceDayPart3Db`), then:
   ```bash
   dotnet tool install --global dotnet-ef
   cd Part3/RaceDay.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
2. **Start the API** (terminal 1, from `Part3/RaceDay.Api`): `dotnet run` → Swagger at http://localhost:5080/swagger
3. **Start the web app locally** (terminal 2, from `Part3/RaceDay.Web`): `dotnet run` → http://localhost:5090

### Run the web app in Docker (single command)

With the API running on your computer (step 2), from the **`Part3`** folder:

```bash
docker compose up --build
```

Then open **http://localhost:8080**. Stop it with `Ctrl+C` (or `docker compose down`).

The same thing without compose:

```bash
docker build -t raceday-web ./RaceDay.Web
docker run --rm -p 8080:8080 -e ApiBaseUrl=http://host.docker.internal:5080 --add-host=host.docker.internal:host-gateway raceday-web
```

`host.docker.internal` means "the computer Docker runs on", where the API is listening. If the container cannot reach the API, start the
API with `dotnet run --urls "http://0.0.0.0:5080"` and allow it through the Windows firewall prompt.

The container behaves identically to running locally — it is the same app calling the same API; only the address of the API is passed in
with the `ApiBaseUrl` setting.

**About the Dockerfile** (`RaceDay.Web/Dockerfile`): a two-stage build. Stage 1 uses the full .NET SDK to restore (cached in its own layer) and
publish a Release build. Stage 2 copies only the published output onto the small ASP.NET Core runtime image, listens on port 8080, and runs
as the image's non-root `app` user. A `.dockerignore` keeps `bin/`, `obj/` and other clutter out of the build.

### Try the whole system
1. Register an **Organiser** (code `RACEDAY-ORGANISER-2026`) and log in → create an event **with a banner image** → add categories → add a route.
2. In a private browser window register a **Participant**, browse, filter, open the event, **enrol**, and upload a **profile picture**.
3. Back as the Organiser: view the **enrolments**, enter the participant's **result**.
4. As the Participant: **My results** shows the position out of all finishers.

## Tests

`dotnet test Part3/RaceDay.sln` runs **85** automated tests against the API (registration and login, event management, role rejection,
enrolments, results, the dashboard, image upload with a fake blob store, storage-failure handling, and a test that Swagger exposes all
**31** endpoints). The web app has no database or business logic to test; its pages were driven end to end against the running API
(login, role redirects, enrolling, results, uploads, expired sessions and API outages).

## Changes from Part 2

The Part 3 brief needed more from the API, so the API was extended (the submitted `Part2/` folder is untouched).

| Change | Why |
|---|---|
| `GET /api/events/mine` | Organiser dashboard with enrolment counts |
| `POST` / `GET /api/events/{id}/banner` | Event banner images in Azure Blob Storage |
| `POST` / `GET /api/users/me/picture` | Profile pictures in Azure Blob Storage |
| Enrolment responses carry `eventDate`, `resultId`, `finishTime`, `position` | Results-entry page and "my enrolments" |
| Result responses carry `eventDate` and `totalFinishers` | "47th out of 312 finishers" |
| `Users.ProfilePictureBlob`, `Events.BannerImageBlob` columns | Link rows to their blobs |

See [`docs/API_Endpoint_Plan_v3.md`](docs/API_Endpoint_Plan_v3.md), [`docs/RaceDay_ERD_v3.png`](docs/RaceDay_ERD_v3.png) and
[`docs/RaceDay_Schema_v3.sql`](docs/RaceDay_Schema_v3.sql).

## Folder structure

```
Part3/
├── RaceDay.sln
├── docker-compose.yml
├── RaceDay.Api/          REST API (Part 2 + Blob Storage + dashboard/result data)
├── RaceDay.Api.Tests/    85 xUnit tests
├── RaceDay.Web/          MVC application
│   ├── Controllers/      Home, Account, Organiser, Participant, Media
│   ├── Models/           API data shapes + form models with validation
│   ├── Services/         RaceDayApiClient, session helpers, role filter
│   ├── Views/            Razor views (shared layout with role-based navigation)
│   ├── wwwroot/          RaceDay theme (CSS, JS)
│   └── Dockerfile
└── docs/                 v3 ERD, SQL script, endpoint plan
```

## CI/CD

`.github/workflows/part2-build-test.yml` runs on every push. It builds and tests the Part 2 API, then builds the Part 3 API and MVC
web app, runs the tests, and builds the Docker image.

**Green build screenshot:**

<img width="1920" height="1080" alt="Part3 Green build" src="https://github.com/user-attachments/assets/82778c6e-a2f4-4875-bb86-d9da99f87693" />


## Video walkthrough

> _[Insert unlisted YouTube link here]_
