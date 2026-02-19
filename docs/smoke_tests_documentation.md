# GWT SMOKE TESTS

## Prerequisites

1. Start the **API project** locally.  
2. Verify that the base URL matches the configured environment:

   ```
   @baseUrl = http://localhost:5141
   ```

3. Ensure seeded users and categories exist in the database:
   - `admin / Admin123!`
   - `john_doe / User123!`
   - Categories: *General*, *Programming*, *News*, *Gaming*, *Sports*

### Note: The response variables are configured for Rider IDE.

---

## AUTH ENDPOINTS (`/api/auth`)

### **Scenario 1: Register a New User**
**Given:** Valid registration data with a unique username  
**When:** `POST /api/auth/register` is called  
**Then:** `200 OK` with `userId` and `token` in response  

```http
POST {{baseUrl}}/api/auth/register
Content-Type: application/json

{
  "username": "smoketest_user",
  "email": "smoketest@example.com",
  "password": "Test123!"
}
```

Response variables:
```js
client.global.set("newUserId", response.body.userId);
client.global.set("newUserToken", response.body.token);
```

---

### **Scenario 2: Login as Admin**
**Given:** Seeded admin user exists (`admin / Admin123!`)  
**When:** `POST /api/auth/login` is called  
**Then:** `200 OK` with JWT token, userId, and username  

```http
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin123!"
}
```

Response variables:
```js
client.global.set("adminToken", response.body.token);
client.global.set("adminUserId", response.body.userId);
```

---

### **Scenario 3: Login as Regular User**
**Given:** Seeded user (`john_doe / User123!`)  
**When:** `POST /api/auth/login` is called  
**Then:** `200 OK` with JWT token, userId, and username  

```http
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{
  "username": "john_doe",
  "password": "User123!"
}
```

Response variables:
```js
client.global.set("regularToken", response.body.token);
client.global.set("regularUserId", response.body.userId);
```

---

## CATEGORY ENDPOINTS (`/api/categories`)

### **Scenario 4: Get All Categories**
**Given:** Seeded categories exist  
**When:** `GET /api/categories` is called without authentication  
**Then:** `200 OK` with array of category objects  

```http
GET {{baseUrl}}/api/categories
Accept: application/json
```

---

### **Scenario 5: Create Category as Admin**
**Given:** Authenticated as admin  
**When:** `POST /api/categories`  
**Then:** `201 Created` with new category object  

```http
POST {{baseUrl}}/api/categories
Content-Type: application/json
Authorization: Bearer {{adminToken}}

{
  "name": "Smoke Test Category"
}
```

---

### **Scenario 6: Create Category as Regular User (Forbidden)**
**Given:** Authenticated as regular user  
**When:** `POST /api/categories`  
**Then:** `403 Forbidden` (RequireRole("Admin"))  

```http
POST {{baseUrl}}/api/categories
Content-Type: application/json
Authorization: Bearer {{regularToken}}

{
  "name": "Should Not Be Created"
}
```

---

## THREAD ENDPOINTS (`/api/threads`)

### **Scenario 7: Get All Threads (Paginated)**
**Given:** Seeded threads exist  
**When:** `GET /api/threads`  
**Then:** `200 OK` with paged result  

```http
GET {{baseUrl}}/api/threads
Accept: application/json
```

---

### **Scenario 8: Get Thread by ID**
**Given:** Thread with ID `1` exists  
**When:** `GET /api/threads/1`  
**Then:** `200 OK` with detailed thread data  

```http
GET {{baseUrl}}/api/threads/1
Accept: application/json
```

---

### **Scenario 9: Create Thread as Authenticated User**
**Given:** `john_doe` is authenticated  
**When:** `POST /api/threads`  
**Then:** `201 Created` with thread object  

```http
POST {{baseUrl}}/api/threads
Content-Type: application/json
Authorization: Bearer {{regularToken}}

{
  "title": "Smoke Test Thread",
  "categoryId": 1,
  "body": "This is the body of the smoke test thread."
}
```

---

## COMMENT ENDPOINTS (`/api/comments`)

### **Scenario 10: Get Comments by Thread**
**Given:** Thread `1` has seeded comments  
**When:** `GET /api/comments/thread/1`  
**Then:** `200 OK` with paginated comments  

```http
GET {{baseUrl}}/api/comments/thread/1?pageNumber=1&pageSize=50
Accept: application/json
```

---

### **Scenario 11: Create Comment as Authenticated User**
**Given:** Authenticated as `john_doe`  
**When:** `POST /api/comments`  
**Then:** `201 Created` with new comment object  

```http
POST {{baseUrl}}/api/comments
Content-Type: application/json
Authorization: Bearer {{regularToken}}

{
  "threadId": 1,
  "content": "This is a smoke test comment.",
  "parentCommentId": null
}
```

---

### **Scenario 12: Create Comment Without Authentication**
**Given:** No token provided  
**When:** `POST /api/comments`  
**Then:** `401 Unauthorized`  

```http
POST {{baseUrl}}/api/comments
Content-Type: application/json

{
  "threadId": 1,
  "content": "This should fail.",
  "parentCommentId": null
}
```

---

## USER ENDPOINTS (`/api/users`)

### **Scenario 13: Get User Profile by Existing ID**
**Given:** Admin user exists  
**When:** `GET /api/users/{adminUserId}`  
**Then:** `200 OK` with user profile  

```http
GET {{baseUrl}}/api/users/{{adminUserId}}
Accept: application/json
```

---

### **Scenario 14: Update Own Profile**
**Given:** `john_doe` is authenticated  
**When:** `PUT /api/users/{regularUserId}`  
**Then:** `200 OK` with updated profile  

```http
PUT {{baseUrl}}/api/users/{{regularUserId}}
Content-Type: application/json
Authorization: Bearer {{regularToken}}

{
  "userName": "john_doe",
  "email": "john_updated@example.com"
}
```

---

### **Scenario 15: Get Non-Existent User Profile**
**Given:** No user with the given ID exists  
**When:** `GET /api/users/nonexistent-id-12345`  
**Then:** `404 Not Found`  

```http
GET {{baseUrl}}/api/users/nonexistent-id-12345
Accept: application/json
```

---

## Summary

| # | Endpoint | Description | Expected Status |
|:-:|-----------|--------------|-----------------|
| 1 | `/api/auth/register` | Register new user | 200 |
| 2 | `/api/auth/login` | Login as admin | 200 |
| 3 | `/api/auth/login` | Login as user | 200 |
| 4 | `/api/categories` | Get all categories | 200 |
| 5 | `/api/categories` | Create category (admin) | 201 |
| 6 | `/api/categories` | Create category (user) | 403 |
| 7 | `/api/threads` | Get threads | 200 |
| 8 | `/api/threads/{id}` | Get thread by ID | 200 |
| 9 | `/api/threads` | Create thread | 201 |
| 10 | `/api/comments/thread/{id}` | Get comments | 200 |
| 11 | `/api/comments` | Create comment | 201 |
| 12 | `/api/comments` | Create comment unauthenticated | 401 |
| 13 | `/api/users/{id}` | Get user profile | 200 |
| 14 | `/api/users/{id}` | Update own profile | 200 |
| 15 | `/api/users/nonexistent` | Get non-existent user | 404 |
