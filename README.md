# ChatForum_GroupTwo

A Blazor Server forum application built with .NET 8, Entity Framework Core, and ASP.NET Core Identity.

## ER Diagram

<img width="1100" height="560" alt="ER" src="https://github.com/user-attachments/assets/10e95f4a-556e-45f2-a92c-21220b9917c5" />
<br>
<br>

**Entities:** User, Category, Thread, Comment, (Admin?)

**Notes:**
- Maybe Admin is a role (via Identity) and not a separate entity?
- Thread body is the first comment (no Body field on Thread) – and comments don't have a header.
- Replies use `ParentCommentId` with flat display (no nested comments for replies). Is this ok, instead of nested replies?
- Int vs Guid for PK?

## Architecture

<img width="300" height="500" alt="Arkitekturdiagram" src="https://github.com/user-attachments/assets/6de69c36-cf0a-4dc2-97b0-b456b54410f6" />


```
Forum.Web (Blazor UI + API)
    ↓
Forum.Application (Services, DTOs)
    ↓
Forum.Infrastructure (EF Core, DbContext)
    ↓
Forum.Domain (Entities)
```

## File Structure


```
src/
├── Forum.Domain/
│   └── Entities/
│       ├── User.cs
|       ├── Category.cs
|       ├── Thread.cs 
|       ├── Comment.cs
│
├── Forum.Application/
│   ├── Interfaces/
|       ├── ICategoryService.cs
|       ├── IThreadService.cs
|       ├── ICommentService.cs
│   ├── Services/
|       ├── CategoryService.cs
|       ├── ThreadService.cs
|       ├── CommentService.cs
│   └── DTOs/
|       ├── CategoryDto.cs 
|       ├── ThreadDto.cs
|       ├── CommentDto.cs
|       ├── UserDto.cs 
|      
│
├── Forum.Infrastructure/
│   ├── Data/
│   │   ├── ForumDbContext.cs
│   │   └── DbSeeder.cs
│   └── Migrations/
│
└── Forum.Web/
    ├── Components/
    │   ├── Pages/
    │   ├── Layout/
    │   └── Shared/
    └── wwwroot/

```
## API Contract

This specification defines the communication between the Blazor frontend and the Backend API.

### 1. Categories

Used to list and filter the forum's subject areas.

* **GET `/api/categories**`
* **Description:** Retrieves all available categories (e.g., "Programming", "Leisure").
* **Response (200 OK):** A list of `CategoryDto`.


* **GET `/api/categories/{id}**`
* **Description:** Retrieves details for a specific category.



### 2. Threads

Manages posts within the categories.

* **GET `/api/threads?categoryId={int}**`
* **Description:** Retrieves all threads belonging to a specific category.
* **Response (200 OK):** A list of `ThreadSummaryDto`.


* **GET `/api/threads/{id}**`
* **Description:** Retrieves a specific thread including its content.


* **POST `/api/threads**`
* **Description:** Creates a new thread. **Requires Authentication.**
* **Request Body:** `CreateThreadRequest`.


* **PUT/DELETE `/api/threads/{id}**`
* **Description:** Updates or deletes a thread. **Restricted to the owner (Author).**



### 3. Comments – Including "Replies to comments"

This section handles the hierarchical structure.

* **GET `/api/threads/{threadId}/comments**`
* **Description:** Retrieves all comments for a thread. Returned in a tree structure (`Replies`).
* **Response (200 OK):** A list of `CommentDto`.


* **POST `/api/comments**`
* **Description:** Creates a new comment.
* **Request Body:** `CreateCommentRequest` (Contains `ParentCommentId` if it is a reply to another comment).



### 4. Authentication (Identity)

The contracts for user management are structured as follows:

* **POST `/api/identity/register**`
* **Body:** `RegisterUserRequest` (Username, Email, Password).


* **POST `/api/identity/login**`
* **Body:** `LoginUserRequest` (Email, Password).
* **Response:** JWT Token or Cookie confirmation.



---

### Data Structures (Text Models)

The following DTOs (Data Transfer Objects) are used in the contract:

#### **ThreadSummaryDto:**

* `Id` (Int)
* `Title` (String)
* `AuthorName` (String)
* `CreatedAt` (DateTime)
* `CommentCount` (Int)

#### **CommentDto:** (Crucial for the assignment requirements)

* `Id` (Int)
* `Text` (String)
* `AuthorName` (String)
* `CreatedAt` (DateTime)
* `ParentCommentId` (Int, can be null)
* `Replies` (List of `CommentDto`)

## API Example

**Create Thread**
```http
POST /api/threads
Content-Type: application/json

{
  "title": "Discussion about clean architecture",
  "categoryId": 1,
  "content": "What are your thoughts on..."
}
```

**Response 201 Created**
```json
{
  "threadId": 1,
  "title": "Discussion about clean architecture",
  "username": "johndoe",
  "categoryName": "Technology",
  "timeCreated": "2026-01-28T15:00:00Z"
}
```
