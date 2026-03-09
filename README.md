# ChatForum_GroupTwo

A Clean Architecture implementation of a Sports Forum, built with .NET 8, Entity Framwork Core and ASP.NET Core Identity.

## Getting Started

### Prerequisites

- .NET 8 SDK

### Running the Application

Both the API and the Blazor frontend must run simultaneously in separate terminals. Start the API first.

**1. Build the solution:**
```
dotnet build Forum.sln
```

**2. Start the API (terminal 1):**
```
dotnet run --project src/Forum.Api/Forum.Api.csproj
```
The API starts at `http://localhost:5141`. Swagger UI is available at `http://localhost:5141/swagger`.
The SQLite database is created and seeded automatically on first run.

**3. Start the Blazor frontend (terminal 2):**
```
dotnet run --project src/Forum.Blazor/Forum.Blazor.csproj
```
Blazor starts at `http://localhost:5159`.

## ER Diagram

<img width="500" height="1000" alt="newnew_ER" src="https://github.com/user-attachments/assets/70f22736-ca51-4157-8b88-c976f27011ae" />
<br><br/>

**Notes:**
- Admin is a role (via Identity) and not a separate entity.
- Thread body is the first comment (no Body field on Thread) – and comments don't have a header.
- Replies use `ParentCommentId` with flat display (no nested comments for replies).

**Delete behaviour:**
- User: Soft-delete (deleting a user does not delete their threads or comments, though should hide their name on them).
- Category: Restricted (cannot delete a category containing threads).
- Thread: Cascade (deleting a thread should also delete all comments within that thread).
- Comment: Soft-delete (deleting a comment should not delete replies to that comment – but replies should refer to it as "deleted").

## Architecture

<img width="400" height="1000" alt="newnew_architecture" src="https://github.com/user-attachments/assets/894f04f9-c7c0-4d35-a708-17f5ee7d6b51" />

<br>

**Dependency rules:**
```
Dependency rules:
- Domain has no dependencies (pure entities).
- Application depends on Domain; defines repository interfaces, DTOs, and CQRS handlers.
- Infrastructure implements Application interfaces using EF Core and Identity.
- Api depends on Application (sends MediatR commands/queries).
- Blazor calls the Api over HTTP and shares Application DTOs.
```

## File Structure
```
ChatForum_GroupTwo/
├── Forum.sln
├── forum.db                          # SQLite
└── src/
    ├── Forum.Domain/
    │   └── Entities/                 # User, Category, Thread, Comment
    ├── Forum.Application/
    │   ├── Common/                   # Result, PagedResult, interfaces
    │   ├── DTOs/                     # Auth, Category, Thread, Comment, User
    │   ├── Features/                 # CQRS Commands and Queries (MediatR)
    │   ├── Interfaces/               # Repository interfaces
    │   └── Mappings/                 # Entity-to-DTO mapping extensions
    ├── Forum.Infrastructure/
    │   ├── Data/                     # ForumDbContext, DbSeeder, Migrations
    │   ├── Repositories/             # EF Core repository implementations
    │   └── Services/                 # AuthService (JWT generation)
    ├── Forum.Api/
    │   ├── Endpoints/                # Auth, Categories, Threads, Comments, Users
    │   ├── Extensions/               # ClaimsPrincipal helpers, ProblemDetails mapping
    │   └── Program.cs                # App bootstrap, DI, JWT config, seeding
    └── Forum.Blazor/
        ├── Components/Pages/         # Home, CategoryThreads, ThreadDetails, CreateThread,
        │                             # Login, Register, Logout, Admin
        ├── Services/                 # API client services, auth state provider
        └── wwwroot/                  # Static assets, category images
```
  
## Seeded Data

The database is seeded automatically on first run with the following test data.

### Users

| Username     | Password    | Email              | Role  |
|--------------|-------------|--------------------|-------|
| `admin`      | `Admin123!` | admin@forum.com    | Admin |
| `john_doe`   | `User123!`  | john@example.com   | —     |
| `jane_smith` | `User123!`  | jane@example.com   | —     |

### Categories and Threads (one per category)

| Category   | Thread Title                                          | Author     |
|------------|-------------------------------------------------------|------------|
| Formula 1  | 2025 Season Predictions - Who takes the championship? | john_doe   |
| Football   | Champions League Semi-Finals Discussion               | jane_smith |
| Basketball | NBA Playoffs - Who's making it out of the West?       | john_doe   |
| Tennis     | Is Sinner the new GOAT?                               | jane_smith |
| Hockey     | Stanley Cup contenders this year                      | admin      |
| Golf       | Masters 2025 - Early favorites?                       | john_doe   |
| Cycling    | Tour de France route looks insane this year           | jane_smith |
| Rugby      | Six Nations 2025 - Ireland vs France was incredible   | admin      |

### Comments

Each thread has 2–3 comments. The first comment serves as the thread body. One nested reply is seeded on the Formula 1 thread to demonstrate the reply feature.

## User Scenarios

**Scenario 1 – Registration and discussion participation**
As a new visitor, I want to be able to register an account and post a comment in a thread,
so that I can participate in sports discussions and share my opinion with other users.

**Scenario 2 – Comment Voting**
As a logged in user, I want to be able to upvote or downvote comments,
so that I can show whether I agree and help ensure that the best comments
are most prominently displayed with a positive score.

**Scenario 3 – Content Management**
As an administrator, I want to be able to remove inappropriate threads and comments via the admin page,
so that the forum is kept orderly and free from content that violates the rules.

## API examples

**Register:**
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```
```json
{ "token": "eyJhbGci...", "userId": "a1b2c3...", "username": "johndoe", "email": "john@example.com" }
```

**Login:**
```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "johndoe",
  "password": "SecurePass123!"
}
```
```json
{ "token": "eyJhbGci...", "userId": "a1b2c3...", "username": "johndoe", "email": "john@example.com" }
```

**Create Thread** (the `body` becomes the first comment):
```http
POST /api/threads
Authorization: Bearer <token>
Content-Type: application/json

{
  "title": "Discussion about clean architecture",
  "categoryId": 1,
  "body": "What are your thoughts on..."
}
```

---

## VG Track: Voting (Track 1)

Logged-in users can upvote (▲) or downvote (▼) comments. The score is shown between the arrows, coloured green (positive), red (negative) or grey (zero). Clicking the same arrow undoes the vote; clicking the opposite switches it. Guests see the arrows but cannot interact since you need to be logged in.

The feature spans all layers: 
- `Vote` entity and `IVoteRepository` in Domain/Application
- `CastVoteCommand` handler, a `POST /api/comments/{commentId}/votes`
- Endpoint
- Vote state loaded per-user when fetching comments.
- An EF Core migration (`AddVoteEntity`) adds the Votes table with a unique index on `(UserId, CommentId)`.

