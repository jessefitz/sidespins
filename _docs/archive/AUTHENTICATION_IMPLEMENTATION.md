# SideSpins Authentication Implementation - Phase 1

## What We've Built

### Backend (Azure Functions)
1. **Authentication Models** (`AuthModels.cs`) - Data structures for authentication requests/responses and user claims
2. **Authentication Service** (`AuthService.cs`) - Core service handling Stytch API integration and JWT operations
3. **Authentication Functions** (`AuthFunctions.cs`) - HTTP endpoints for authentication flow
4. **Program.cs Updates** - Dependency injection setup for authentication services

### Frontend (Jekyll)
1. **Login Page** (`/login/`) - SMS and email authentication UI with Stytch integration
2. **Callback Page** (`/auth/callback.html`) - Handles magic link authentication 
3. **App Page** (`/app/`) - Protected content demonstrating authentication state

### Authentication Endpoints
- `POST /api/auth/session` - Exchange Stytch session JWT for app session cookie
- `POST /api/auth/logout` - Clear authentication session
- `GET /api/auth/user` - Get current authenticated user info

## Next Steps to Get It Working

### 1. Set up Stytch Account and Configuration

1. **Sign up for Stytch** at https://stytch.com if you haven't already
2. **Create a new project** in your Stytch dashboard
3. **Get your credentials**:
   - Project ID (public)
   - Secret key (private)
   - Public token (for frontend)

4. **Configure Stytch settings**:
   - Enable SMS OTP authentication
   - Enable Email Magic Links
   - Set redirect URLs to include `http://localhost:4000/auth/callback.html`

### 2. Update Configuration Files

**Update `functions/local.settings.json`:**
```json
{
  "STYTCH_PROJECT_ID": "project-test-your-actual-project-id",
  "STYTCH_SECRET": "secret-test-your-actual-secret",
  "JWT_SIGNING_KEY": "your-jwt-signing-key-needs-to-be-at-least-32-characters-long-for-security"
}
```

**Update frontend files** with your Stytch public token:
- In `docs/login.md` line 59: Replace `YOUR_STYTCH_PUBLIC_TOKEN`
- In `docs/auth/callback.html` line 16: Replace `YOUR_STYTCH_PUBLIC_TOKEN`

### 3. Generate a JWT Signing Key

Run this in your terminal to generate a secure key:
```powershell
[System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Guid]::NewGuid().ToString() + [System.Guid]::NewGuid().ToString()))
```

### 4. Test the Implementation

1. **Start your Functions app**:
   ```powershell
   cd functions
   func start
   ```

2. **Start Jekyll**:
   ```powershell
   cd docs
   bundle exec jekyll serve --livereload
   ```

3. **Test the flow**:
   - Go to `http://localhost:4000/login/`
   - Try SMS authentication with your phone number
   - Try email authentication with your email
   - Verify you can access `http://localhost:4000/app/` after login

### 5. Set Up Stytch User Metadata

For the authentication to work completely, users need the trusted metadata structure defined in the spec. You'll need to either:

**Option A: Set up via Stytch Dashboard**
1. Go to your Stytch dashboard
2. Find the Users section 
3. Add trusted metadata to a test user in this format:
```json
{
  "teams": {
    "team": {
      "team_id": "break_of_dawn_9b",
      "team_role": "manager"
    }
  }
}
```

**Option B: Programmatically via API**
We can add an endpoint to set user metadata after authentication (this would be a future enhancement).

## Current Limitations & Notes

1. **SSL/HTTPS**: For production, you'll need HTTPS. The current setup works for local development.

2. **User Onboarding**: New users won't have team metadata initially. You'll need a flow to assign users to teams.

3. **Error Handling**: The current implementation has basic error handling. You may want to enhance this based on your needs.

4. **Security**: The implementation follows the security practices from your spec (HttpOnly cookies, CORS, etc.).

## Testing Without Full Stytch Setup

If you want to test the backend without setting up Stytch completely, you can:

1. Create a test endpoint that bypasses Stytch validation
2. Manually create a JWT with test claims
3. Test the cookie setting and retrieval flow

Let me know if you'd like me to create a test endpoint for this purpose!

## Files Modified/Created

### Backend
- `functions/functions.csproj` - Added JWT and HTTP client packages
- `functions/Program.cs` - Added authentication service registration
- `functions/local.settings.json` - Added Stytch configuration placeholders
- `functions/AuthModels.cs` - NEW: Authentication data models
- `functions/AuthService.cs` - NEW: Core authentication service
- `functions/AuthFunctions.cs` - NEW: Authentication HTTP endpoints

### Frontend  
- `docs/login.md` - NEW: Login page with Stytch integration
- `docs/auth/callback.html` - NEW: Magic link callback handler
- `docs/app.md` - NEW: Protected app page demonstrating auth state
