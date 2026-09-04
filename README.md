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
