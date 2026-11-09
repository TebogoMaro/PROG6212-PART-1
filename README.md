# RaceDay

RaceDay is a full-stack, web-based event management system built for the
South African road running, walking, and cycling community. It gives Event
Organisers the tools to create and manage events, categories, and participant
results, while Participants can browse upcoming events, enter events, track
their personal performance history, and prepare for race day using live
weather and route information.

This repository contains **Part 1** of the PROG6212 Portfolio of Evidence:
system planning, the database design, and the API endpoint plan for RaceDay.

## Roles

RaceDay supports two distinct user roles:

- **Organiser** — can create, edit, and delete events, manage event
  categories, capture participant results, and view all event enrolments.
- **Participant** — can create an account, browse events, enter an event by
  selecting a category, view their own enrolments, and track their personal
  results.

Role-based access is enforced at the API level in Part 2 and reflected
consistently in the MVC interface in Part 3.

## Repository Structure

```
/docs
  PROG ERD PART 1           - Entity Relationship Diagram (Section A)
  PROG ERD PART 1.pdf            - Entity Relationship Diagram (PDF version)
  PROG ENDPOINT PLAN PART 1.pdf       - Full API endpoint plan (Section B)
  RaceDay_Schema.sql         - SQL Server schema + seed data (Section C)
.github/workflows
  validate-docs.yml          - CI workflow that checks the /docs folder
README.md                    - This file
```

## Database Design (Section A)

The data model consists of six entities: `Users`, `Events`, `Categories`,
`Routes`, `EventEnrolments`, and `Results`. 


## API Endpoint Plan (Section B)



## Database Script (Section C)

 The script creates the full schema matching the ERD exactly, and seeds the database with 2
Organisers, 2 Participants, 3 Events, categories for each event, and sample
enrolments. Run it in SQL Server Management Studio (SSMS) on a clean instance.

## CI/CD

A GitHub Actions workflow (`.github/workflows/validate-docs.yml`) runs on
every push and validates that the `/docs` folder exists and contains the
required planning documents (ERD, endpoint plan, SQL script), and that this
README is present.

**CI/CD screenshot:**

<img width="1920" height="1080" alt="Screenshot (243)" src="https://github.com/user-attachments/assets/e577f208-abfd-4132-b6fa-cfc9c5469883" />


## Video Walkthrough

> https://youtu.be/qLbClqXBEm0?si=S3xtqMATSB2q-0Ia
>
> The video walks through the planning documents, the ERD decisions, the
> endpoint plan choices, and runs the SQL script live in SSMS.


# RaceDay — Part 2: RESTful API

ASP.NET Core Web API (C#) for **RaceDay**, an event management system for South African road
running, walking and cycling events. It implements the endpoint plan from Part 1 (see
[`docs/API_Endpoint_Plan_v2.md`](docs/API_Endpoint_Plan_v2.md)), stores data in **SQL Server** through
**Entity Framework Core (Code-First)**, secures it with **role-based session authentication**,
documents it with **Swagger**, and is covered by **xUnit tests** that run in **GitHub Actions**.

## The two roles

| Role | Can do |
|---|---|
| **Organiser** | Create, edit and delete **their own** events; define categories (distance or age) and routes; view all enrolments for their events; capture and correct results. |
| **Participant** | Register, browse events/categories/routes/weather, enrol in an event by choosing a category, view and cancel their own enrolments, and view their own results. |

Role rules are enforced **on the server** with `[Authorize(Roles = ...)]` plus ownership checks (an Organiser
cannot touch another Organiser's event; a Participant cannot see another Participant's result).
Unauthenticated calls to protected endpoints get **401**; the wrong role gets **403**.

## Setup and run

**Prerequisites:** .NET 8 SDK (or newer), SQL Server / SQL Server Express / LocalDB, Visual Studio 2022+ or VS Code.

1. **Open** `Part2/RaceDay.sln`.
2. **Connection string** — edit `Part2/RaceDay.Api/appsettings.json` → `ConnectionStrings:RaceDayDb` to point at *your* SQL Server
   (the server name shown when you connect in SSMS). The default uses LocalDB. The database is called
   `RaceDayApiDb` — it is created for you, and is separate from the Part 1 `RaceDayDB` script database.
3. **Create the database (EF Core migrations)** — from inside the `Part2/RaceDay.Api` folder:
   ```bash
   dotnet tool install --global dotnet-ef        # once
   cd Part2/RaceDay.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
   (Visual Studio: *Package Manager Console* → `Add-Migration InitialCreate` then `Update-Database`.)
   When run in the Development environment, the API also applies migrations automatically at startup.
4. **Run:** `dotnet run` from `Part2/RaceDay.Api` (or press F5 in Visual Studio). Swagger opens at `http://localhost:5080/swagger`.
5. **Run the tests:** `dotnet test Part2/RaceDay.sln`.

### Trying it in Swagger
1. `POST /api/auth/register` — register an Organiser (`role: "Organiser"`, `organiserCode: "RACEDAY-ORGANISER-2026"`) and a Participant.
2. `POST /api/auth/login` — log in. A session cookie is set and Swagger sends it automatically on later calls.
3. As the Organiser: `POST /api/events`, `POST /api/events/{id}/categories`, `PUT /api/categories/{id}/route`.
4. `POST /api/auth/logout`, log in as the Participant: `POST /api/categories/{id}/enrolments`, `GET /api/enrolments/me`.
5. Log back in as the Organiser: `POST /api/enrolments/{id}/results`; the Participant can then `GET /api/users/me/results`.
6. Try something forbidden (a Participant calling `POST /api/events`) to see the **403**.

## Design decisions

- **Session authentication** — `POST /api/auth/login` issues an encrypted, HTTP-only cookie (`RaceDay.Session`) that carries the
  user's id and role for every later request (2 hours, sliding). For an API the cookie handler returns 401/403 instead of redirecting.
- **Passwords** — hashed with PBKDF2-HMAC-SHA256 (600,000 iterations, random 16-byte salt per user). Plain text is never stored or returned.
- **Role at registration** — the role is chosen at registration, but only `Organiser` / `Participant` are accepted and
  registering as an Organiser needs an **organiser registration code** (`Registration:OrganiserCode` in `appsettings.json`),
  so nobody can simply promote themselves.
- **Validation** — data annotations on every request model produce automatic 400 responses; business rules (full category, duplicate
  enrolment, event in the past, ownership) return 409/403/400 with a clear message.
- **Swagger** — XML comments on every endpoint describe its purpose, the data it expects and the responses it returns.
- **Weather** — `GET /api/events/{id}/weather` uses the free Open-Meteo service (no key); daily forecasts exist for the next 16 days.
- **Tests** — integration tests run the real API in memory (EF Core InMemory, fake weather service). A `SwaggerTests` test fails if any of the
  26 planned endpoints is missing, so the built API provably matches the plan.

## Changes from Part 1 (explained)

The Part 2 brief added requirements the Part 1 design did not capture, so the schema and plan were updated. The v2 documents are in
[`docs/`](docs): `RaceDay_ERD_v2.png/.pdf`, `RaceDay_Schema_v2.sql`, `API_Endpoint_Plan_v2.md`. The EF Core model matches them exactly.

| Change | Why |
|---|---|
| `Events` gains `EventType` (Run/Walk/Cycle) and `DistanceKm` | Part 2: every event must capture distance and event type |
| `Categories.DistanceKm` became optional | Age categories such as "Under 20" / "Senior" have no distance |
| `EventEnrolments` gains `EventId` and `UNIQUE(ParticipantId, EventId)` | Part 2: the enrolment must record participant, event **and** category; one entry per event |
| Login starts a session; `POST /api/auth/logout` added | Part 2 requires session management (Part 1 said "token") |
| Organiser registration code; extra 403/409 responses | See design decisions and the change table in the endpoint plan |
| Part 1 endpoint count corrected (24 → 25 → 26 with logout) | Counting error in Part 1 |

## CI/CD

`.github/workflows/part2-build-test.yml` restores, builds and runs all unit tests on every push.

**Green build screenshot:**

> <img width="1920" height="1080" alt="part2succesfulgreenbuild" src="https://github.com/user-attachments/assets/0d4afafc-f362-4788-bc6a-a70a982608ef" />



## Video walkthrough

> https://youtu.be/9Rywja6u5YQ


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

> https://youtu.be/yvN2ZytVITQ


