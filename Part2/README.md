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
