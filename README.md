# ChatForum_GroupTwo

A Blazor Server forum application built with .NET 8, Entity Framework Core, and ASP.NET Core Identity.

## ER Diagram

<img width="950" height="1500" alt="ER_new" src="https://github.com/user-attachments/assets/f2357693-a4bc-4c03-b1e7-c575ce45dff6" />
<br>
<br>

**Entities:** User, Category, Thread, Comment

**Notes:**
- Admin is a role (via Identity) and not a separate entity.
- Thread body is the first comment (no Body field on Thread) – and comments don't have a header.
- Replies use `ParentCommentId` with flat display (no nested comments for replies).

**Delete behaviour:**
- User: Soft-delete (deleting a user does not delete their threads or comments, though should hide their name on them).
- Category: Cascade (deleting a category should also delete threads within that category).
- Thread: Cascade (deleting a thread should also delete all comments within that thread).
- Comment: Restricted (or soft-delete?) (deleting a comment should not delete replies to that comment – but replies should refer to it as "deleted").

## Architecture

<img width="400" height="1000" alt="Arkitekt_excali" src="https://github.com/user-attachments/assets/adf3711e-6d27-418f-8fa2-0a4336bed543" />



**Dependency rules:**

```
Domain: No dependencies (pure entities).
Application: Depends on Domain, defines interfaces.
Api: Depends on Application and Domain.
Infrastructure: Depends on Application and Domain, implements interfaces.
Blazor: Depends on Application (DTOs), calls Api via HTTP.
```

## File Structure

<img width="257" height="815" alt="tree" src="https://github.com/user-attachments/assets/50cfecf9-5193-4911-8dcd-61617cb4091e" />
  

## API Contract Examples

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

**Response 200 OK**
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "username": "johndoe",
  "email": "john@example.com"
}
```

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

**Response 200 OK**
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "username": "johndoe",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

### Create Category
```http
POST /api/categories
Content-Type: application/json

{
  "name": "Technology"
}
```

**Response 201 Created**
```json
{
  "categoryId": 1,
  "name": "Technology",
  "threadCount": 0
}
```

### Create Thread
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

### Create Comment
```http
POST /api/threads/1/comments
Content-Type: application/json

{
  "content": "Great topic! I think...",
  "parentCommentId": null
}
```

**Response 201 Created**
```json
{
  "commentId": 2,
  "content": "Great topic! I think...",
  "username": "janedoe",
  "parentCommentId": null,
  "timeCreated": "2026-01-28T16:30:00Z"
}
```

### Reply to Comment
```http
POST /api/threads/1/comments
Content-Type: application/json

{
  "content": "I agree with your point!",
  "parentCommentId": 2
}
```

**Response 201 Created**
```json
{
  "commentId": 3,
  "content": "I agree with your point!",
  "username": "johndoe",
  "parentCommentId": 2,
  "parentUsername": "janedoe",
  "timeCreated": "2026-01-28T17:00:00Z"
}
```
