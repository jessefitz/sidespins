# Authentication Quick Reference

## For Function Developers

### Adding Authentication to a New Endpoint

```csharp
[Function("YourFunctionName")]
[RequireAuthentication("player")] // or "manager" or "admin"
public async Task<IActionResult> YourFunction(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
    FunctionContext context) // <- Add this parameter
{
    // Get user information
    var userClaims = context.GetUserClaims();
    var userId = context.GetUserId();
    var teamId = context.GetTeamId();
    var role = context.GetTeamRole();
    
    // Your business logic here
}
```

### Role Requirements by Operation Type

| Operation | Required Role | Example |
|-----------|---------------|---------|
| Read data | `player` | GetMatches, GetPlayers |
| Create/Update | `manager` | CreateTeam, UpdatePlayer |
| Delete | `admin` | DeleteTeam, DeletePlayer |

### Team Authorization Pattern

```csharp
// For team-specific operations
var requestedTeamId = req.Query["teamId"];
var userTeamId = context.GetTeamId();

// Non-admins can only access their own team
if (userClaims.TeamRole != "admin" && userTeamId != requestedTeamId)
{
    return new StatusCodeResult(403);
}
```

## For Client Developers

### Authentication Flow

```javascript
// 1. Get token
const authResponse = await fetch('/api/auth/sms/verify', {
    method: 'POST',
    body: JSON.stringify({ phoneId, code })
});
const { sessionToken } = await authResponse.json();

// 2. Use token in requests
const response = await fetch('/api/endpoint', {
    headers: { 'Authorization': `Bearer ${sessionToken}` }
});
```

### Error Handling

```javascript
if (response.status === 401) {
    // Redirect to login
} else if (response.status === 403) {
    // Show "insufficient permissions" message
}
```

### Role-Based UI

```javascript
const claims = JSON.parse(localStorage.getItem('userClaims'));

if (claims.teamRole === 'manager' || claims.teamRole === 'admin') {
    showCreateButton();
}

if (claims.teamRole === 'admin') {
    showDeleteButton();
}
```

## Quick Testing

### Without Authentication (should return 401)
```bash
curl http://localhost:7071/api/GetMemberships?teamId=123
```

### With Authentication
```bash
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     http://localhost:7071/api/GetMemberships?teamId=123
```

## Common Patterns

### Public Endpoint
```csharp
[Function("GetTeams")]
// No [RequireAuthentication] attribute
public async Task<IActionResult> GetTeams(...)
```

### Protected Endpoint
```csharp
[Function("CreateTeam")]
[RequireAuthentication("manager")]
public async Task<IActionResult> CreateTeam(..., FunctionContext context)
```

### Team-Scoped Endpoint
```csharp
[Function("GetTeamPlayers")]
[RequireAuthentication("player")]
public async Task<IActionResult> GetTeamPlayers(
    [HttpTrigger(..., Route = "teams/{teamId}/players")] HttpRequest req,
    FunctionContext context,
    string teamId)
{
    if (!context.IsUserInTeam(teamId) && !context.HasMinimumRole("admin"))
    {
        return new StatusCodeResult(403);
    }
}
```
