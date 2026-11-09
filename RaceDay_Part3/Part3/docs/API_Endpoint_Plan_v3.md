# RaceDay — API Endpoint Plan v3 (Part 3)

This is the Part 2 endpoint plan plus what the Part 3 web application needs. **Total endpoints: 31** (26 from Part 2 + 5 new).
Every endpoint is implemented in `Part3/RaceDay.Api/Controllers` and checked by an automated test (`SwaggerTests`)
that fails if any of them is missing from the running API's Swagger document.

Only the API talks to Azure Blob Storage. The MVC web app never does - it uploads and downloads images through
the API endpoints below.

## Changes from the Part 2 plan

| # | Change | Reason |
|---|---|---|
| 1 | **New** `GET /api/events/mine` | Organiser dashboard: own events with enrolment counts in one call |
| 2 | **New** `POST` + `GET /api/events/{id}/banner` | Part 3: Organisers upload an event banner; stored in Azure Blob Storage and shown on the event page |
| 3 | **New** `POST` + `GET /api/users/me/picture` | Part 3: Participants upload a profile picture |
| 4 | Event and user responses gain `hasBanner` / `hasProfilePicture` | So the UI knows whether an image exists |
| 5 | Enrolment responses gain `eventDate`, `resultId`, `finishTime`, `position` | Results-entry page and "my enrolments" need to know what is already captured |
| 6 | Result responses gain `eventDate` and `totalFinishers` | "My Results" shows the date and "47th out of 312 finishers" |
| 7 | Database: `Users.ProfilePictureBlob`, `Events.BannerImageBlob` (names of blobs, not the images) | Links a row to its file in Azure Blob Storage |

## Endpoints

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new account as an Organiser or a Participant. Password is hashed. | None (public) | `{ fullName, email, password, role, organiserCode }` | 201 Created — new user · 400 validation failed · 403 bad organiser code · 409 email already registered |
| POST | /api/auth/login | Verifies credentials and starts a session (cookie). | None (public) | `{ email, password }` | 200 OK — user details + session cookie · 401 invalid credentials |
| POST | /api/auth/logout | Ends the current session. | Any (logged in) | None | 204 No Content · 401 Unauthorized |
| GET | /api/users/me | Returns the logged-in user's profile. | Any (logged in) | None | 200 OK — profile · 401 |
| PUT | /api/users/me | Updates the logged-in user's name and email. | Any (logged in) | `{ fullName, email }` | 200 OK — profile · 400 · 401 · 409 email taken |
| GET | /api/users/me/results | Personal results history, newest first. | Participant | None | 200 OK — array of results · 401 · 403 |
| GET | /api/events | Lists upcoming events. Filters: location, eventType, fromDate, toDate, includePast. | None (public) | None | 200 OK — array of events · 400 bad filter |
| GET | /api/events/{id} | Full details of one event, including its categories. | None (public) | None | 200 OK — event · 404 |
| POST | /api/events | Creates an event owned by the logged-in Organiser. | Organiser | `{ eventName, eventDate, location, description, eventType, distanceKm }` | 201 Created — event · 400 · 401 · 403 |
| PUT | /api/events/{id} | Updates an event. Only its owner may do this. | Organiser | `{ eventName, eventDate, location, description, eventType, distanceKm }` | 200 OK — event · 400 · 401 · 403 not owner · 404 |
| DELETE | /api/events/{id} | Deletes an event with its categories and routes. | Organiser | None | 204 · 401 · 403 not owner · 404 · 409 has enrolments |
| GET | /api/events/{id}/weather | Live weather forecast for the event's location and date. | None (public) | None | 200 OK — forecast · 404 · 502 weather provider unavailable |
| GET | /api/events/{id}/categories | Lists the categories of an event. | None (public) | None | 200 OK — array · 404 |
| POST | /api/events/{id}/categories | Adds a category (e.g. 10km, Under 20) to an event. | Organiser | `{ categoryName, distanceKm, maxParticipants, entryFee }` | 201 Created — category · 400 · 401 · 403 · 404 |
| GET | /api/events/{id}/enrolments | Lists everyone enrolled in an event. | Organiser | None | 200 OK — array · 401 · 403 not owner · 404 |
| GET | /api/categories/{id} | One category. | None (public) | None | 200 OK — category · 404 |
| PUT | /api/categories/{id} | Updates a category. | Organiser | `{ categoryName, distanceKm, maxParticipants, entryFee }` | 200 OK — category · 400 · 401 · 403 · 404 |
| DELETE | /api/categories/{id} | Removes a category (and its route). | Organiser | None | 204 · 401 · 403 · 404 · 409 has enrolments |
| GET | /api/categories/{id}/route | The route (start, end, description, map) of a category. | None (public) | None | 200 OK — route · 404 |
| PUT | /api/categories/{id}/route | Creates or replaces the route of a category. | Organiser | `{ startPoint, endPoint, routeDescription, mapUrl }` | 200 OK — route · 400 · 401 · 403 · 404 |
| POST | /api/categories/{id}/enrolments | Enrols the logged-in Participant in a category (and its event). | Participant | None (participant comes from the session) | 201 Created — enrolment (participant, event, category) · 401 · 403 · 404 · 409 already enrolled / full / event over |
| GET | /api/enrolments/me | The logged-in Participant's enrolments. | Participant | None | 200 OK — array · 401 · 403 |
| DELETE | /api/enrolments/{id} | Cancels the Participant's own enrolment. | Participant | None | 204 · 401 · 403 not yours · 404 · 409 result already captured |
| POST | /api/enrolments/{id}/results | Captures finish time and position. | Organiser | `{ finishTime, position }` | 201 Created — result · 400 · 401 · 403 not owner · 404 · 409 already captured / cancelled |
| GET | /api/enrolments/{id}/results | The result of one enrolment. | Any (logged in) | None | 200 OK — result · 401 · 403 not yours · 404 none yet |
| PUT | /api/results/{id} | Corrects a captured result. | Organiser | `{ finishTime, position }` | 200 OK — result · 400 · 401 · 403 · 404 |
| GET | /api/events/mine | The logged-in Organiser's own events with category and enrolment counts (powers the dashboard). | Organiser | None | 200 OK — array · 401 · 403 |
| POST | /api/events/{id}/banner | Uploads or replaces an event's banner image. Stored in **Azure Blob Storage**. multipart/form-data, field `file`; JPG/PNG/WebP, max 5 MB. | Organiser | form file `file` | 200 OK — event · 400 bad/oversized file · 401 · 403 not owner · 404 · 502 storage unavailable |
| GET | /api/events/{id}/banner | Downloads the event's banner image (the API reads it from Azure Blob Storage). | None (public) | None | 200 OK — image bytes · 404 no banner · 502 storage unavailable |
| POST | /api/users/me/picture | Uploads or replaces the logged-in user's profile picture (Azure Blob Storage). multipart/form-data, field `file`. | Any (logged in) | form file `file` | 200 OK — profile · 400 · 401 · 502 |
| GET | /api/users/me/picture | Downloads the logged-in user's profile picture. | Any (logged in) | None | 200 OK — image bytes · 401 · 404 none uploaded · 502 |
