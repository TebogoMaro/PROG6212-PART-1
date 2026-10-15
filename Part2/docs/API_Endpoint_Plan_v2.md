# RaceDay — API Endpoint Plan v2 (Part 2)

This is the Part 1 endpoint plan as it was actually built in Part 2. **Total endpoints: 26.**
Every endpoint below is implemented in `RaceDay.Api/Controllers` and checked by an automated
test (`SwaggerTests`) that fails if any of them is missing from the running API's Swagger document.

Authentication is **session based**: `POST /api/auth/login` sets an encrypted, HTTP-only session
cookie holding the user's id and role. A role of "Any" means any logged-in user.

## Changes from the Part 1 plan

| # | Change | Reason |
|---|---|---|
| 1 | Login returns the user and starts a **session cookie** instead of returning a token | Part 2 requires session management |
| 2 | **Added** `POST /api/auth/logout` | A session needs a way to end |
| 3 | Register takes an `organiserCode` for Organisers (403 if wrong) | Stops anyone self-promoting to Organiser ("think about how this should be managed") |
| 4 | Event bodies gain `eventType` and `distanceKm` | Part 2 requires every event to capture them |
| 5 | Category `distanceKm` is optional | Age categories ("Under 20", "Senior") have no distance |
| 6 | `DELETE` event / category return **409** if enrolments exist | Protects participants' data |
| 7 | `DELETE /api/enrolments/{id}` marks the enrolment **Cancelled** (row kept); enrolling again re-activates it | Keeps history; one enrolment per participant per event |
| 8 | `GET /api/enrolments/{id}/results` can return **403** | A participant may only see their own result; an organiser only their own events' results |
| 9 | Corrected the Part 1 total: it said 24, the table actually had 25 | Counting error |

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
