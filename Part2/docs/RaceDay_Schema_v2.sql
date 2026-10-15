/* ============================================================
   RaceDay Database Schema - v2 (Part 2)
   Target: SQL Server (SSMS)

   Matches RaceDay_ERD_v2.png and the Entity Framework Core model
   in RaceDay.Api/Data/RaceDayDbContext.cs. Changes since Part 1:
     - Events:          + EventType (Run/Walk/Cycle), + DistanceKm
     - Categories:      DistanceKm is now NULL-able (age groups such as "Under 20")
     - EventEnrolments: + EventId, and UNIQUE (ParticipantId, EventId)
   NOTE: the API itself creates its database through EF Core migrations
   (database "RaceDayApiDb"). This script is the reference/SSMS version.
   ============================================================ */

IF DB_ID('RaceDayDB_v2') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB_v2;
END
GO

USE RaceDayDB_v2;
GO

IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.EventEnrolments', 'U') IS NOT NULL DROP TABLE dbo.EventEnrolments;
IF OBJECT_ID('dbo.Routes', 'U') IS NOT NULL DROP TABLE dbo.Routes;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users (
    UserId         INT IDENTITY(1,1) PRIMARY KEY,
    FullName       NVARCHAR(100)   NOT NULL,
    Email          NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash   NVARCHAR(255)   NOT NULL,
    Role           VARCHAR(20)     NOT NULL DEFAULT 'Participant'
                       CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant')),
    CreatedAt      DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE dbo.Events (
    EventId        INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId    INT             NOT NULL,
    EventName      NVARCHAR(150)   NOT NULL,
    EventDate      DATE            NOT NULL,
    Location       NVARCHAR(150)   NOT NULL,
    Description    NVARCHAR(1000)  NULL,
    EventType      VARCHAR(10)     NOT NULL
                       CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    DistanceKm     DECIMAL(6,2)    NOT NULL,
    CreatedAt      DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Categories (
    CategoryId       INT IDENTITY(1,1) PRIMARY KEY,
    EventId          INT             NOT NULL,
    CategoryName     NVARCHAR(100)   NOT NULL,
    DistanceKm       DECIMAL(5,2)    NULL,
    MaxParticipants  INT             NOT NULL DEFAULT 100,
    EntryFee         DECIMAL(8,2)    NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventId) REFERENCES dbo.Events(EventId)
);
GO

CREATE TABLE dbo.Routes (
    RouteId          INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId       INT             NOT NULL UNIQUE,
    StartPoint       NVARCHAR(150)   NOT NULL,
    EndPoint         NVARCHAR(150)   NOT NULL,
    RouteDescription NVARCHAR(1000)  NULL,
    MapUrl           NVARCHAR(255)   NULL,
    CONSTRAINT FK_Routes_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
);
GO

CREATE TABLE dbo.EventEnrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    EventId         INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          VARCHAR(20)     NOT NULL DEFAULT 'Confirmed'
                       CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Event       FOREIGN KEY (EventId)       REFERENCES dbo.Events(EventId),
    CONSTRAINT FK_Enrolments_Category    FOREIGN KEY (CategoryId)    REFERENCES dbo.Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_ParticipantEvent UNIQUE (ParticipantId, EventId)
);
GO

CREATE TABLE dbo.Results (
    ResultId       INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId    INT             NOT NULL UNIQUE,
    FinishTime     TIME            NOT NULL,
    Position       INT             NULL,
    CapturedAt     DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.EventEnrolments(EnrolmentId)
);
GO

/* ===================== SEED DATA (reference only) ===================== */

INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role) VALUES
('Sipho Mokoena',       'sipho.mokoena@raceday.co.za',  'HASH_PLACEHOLDER_1', 'Organiser'),
('Lerato Dlamini',      'lerato.dlamini@raceday.co.za', 'HASH_PLACEHOLDER_2', 'Organiser'),
('Johan van der Merwe', 'johan.vdm@example.com',        'HASH_PLACEHOLDER_3', 'Participant'),
('Naledi Khumalo',      'naledi.khumalo@example.com',   'HASH_PLACEHOLDER_4', 'Participant');
GO

INSERT INTO dbo.Events (OrganiserId, EventName, EventDate, Location, Description, EventType, DistanceKm) VALUES
(1, 'Joburg City Marathon',      '2026-11-15', 'Johannesburg, Gauteng', 'Annual road marathon through the Johannesburg CBD.',  'Run',   21.10),
(1, 'Sandton Charity Fun Run',   '2026-10-04', 'Sandton, Gauteng',      'Community charity walk/run in support of local schools.', 'Run',    5.00),
(2, 'Durban Coastal Cycle Tour', '2026-12-06', 'Durban, KwaZulu-Natal', 'Scenic cycling tour along the Durban coastline.',          'Cycle', 40.00);
GO

INSERT INTO dbo.Categories (EventId, CategoryName, DistanceKm, MaxParticipants, EntryFee) VALUES
(1, '10km Run',           10.00, 2000, 150.00),
(1, '21km Half Marathon', 21.10, 1500, 250.00),
(2, '5km Fun Run',         5.00, 1000,  80.00),
(3, '40km Road Cycle',    40.00,  800, 300.00),
(2, 'Under 20',            NULL,  300,  50.00),
(2, 'Senior',              NULL,  300,  50.00);
GO

INSERT INTO dbo.Routes (CategoryId, StartPoint, EndPoint, RouteDescription, MapUrl) VALUES
(1, 'Mary Fitzgerald Square', 'FNB Stadium',     'Flat city route through Newtown and Soweto.', 'https://maps.example.com/route1'),
(2, 'Mary Fitzgerald Square', 'FNB Stadium',     'Extended loop adding Auckland Park before finishing at FNB Stadium.', 'https://maps.example.com/route2'),
(3, 'Sandton Central',        'Sandton Central', 'Loop route around Sandton CBD.', 'https://maps.example.com/route3'),
(4, 'uShaka Marine World',    'Umhlanga Rocks',  'Coastal cycle route along the Durban beachfront.', 'https://maps.example.com/route4');
GO

INSERT INTO dbo.EventEnrolments (ParticipantId, EventId, CategoryId, Status) VALUES
(3, 1, 1, 'Confirmed'),  -- Johan enters the 10km Run (Joburg City Marathon)
(4, 1, 2, 'Confirmed'),  -- Naledi enters the 21km Half Marathon (Joburg City Marathon)
(3, 2, 3, 'Confirmed');  -- Johan enters the 5km Fun Run (Sandton Charity Fun Run)
GO

INSERT INTO dbo.Results (EnrolmentId, FinishTime, Position) VALUES
(1, '00:48:32', 152),
(2, '01:52:10', 47);
GO
