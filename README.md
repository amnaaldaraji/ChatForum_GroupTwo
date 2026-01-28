# ChatForum_GroupTwo

A Blazor Server forum application built with .NET 8, Entity Framework Core, and ASP.NET Core Identity.

## ER Diagram

<img width="350" height="350" alt="bild" src="https://github.com/user-attachments/assets/e9a72147-5bcb-4a36-81a2-56d4afc3697a" />


**Entities:** User, Category, Thread, Comment

**Notes:**
- Maybe Admin is a role (via Identity) and not a separate entity?
- Thread body is the first comment (no Body field on Thread) – and comments don't have a header.
- Replies use `ParentCommentId` with flat display (no nested comments for replies).

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
