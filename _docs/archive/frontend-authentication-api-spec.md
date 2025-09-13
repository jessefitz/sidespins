# Frontend Authentication API Specification

Based on your Azure Functions authentication API, here's a comprehensive specification for how the frontend should handle authentication:

## Base URL
```
http://localhost:7071/api/auth (development)
https://your-function-app.azurewebsites.net/api/auth (production)
```

## Authentication Flow Options

### Option 1: SMS Authentication

**Step 1: Send SMS Code**
```javascript
POST /auth/sms/send
Content-Type: application/json

{
  "phoneNumber": "+14104403238"
}

// Response
{
  "ok": true,
  "message": "SMS code sent successfully"
}
```

**Step 2: Verify SMS Code**
```javascript
POST /auth/sms/verify
Content-Type: application/json

{
  "phoneNumber": "+14104403238",
  "code": "123456"
}

// Response
{
  "ok": true,
  "message": "Authentication successful"
}
// Sets httpOnly cookies: 'ssid' and 'team' (if applicable)
```

### Option 2: Magic Link Authentication

**Step 1: Send Magic Link**
```javascript
POST /auth/email/send
Content-Type: application/json

{
  "email": "user@example.com"
}

// Response
{
  "ok": true,
  "message": "Magic link sent successfully"
}
```

**Step 2: Authenticate Magic Link**
```javascript
POST /auth/email/authenticate
Content-Type: application/json

{
  "token": "magic_link_token_from_email"
}

// Response
{
  "ok": true,
  "message": "Authentication successful"
}
// Sets httpOnly cookies: 'ssid' and 'team' (if applicable)
```

## Session Management

**Check Current User**
```javascript
GET /auth/user

// Response (authenticated)
{
  "userId": "user-123",
  "teamId": "team-456",
  "teamRole": "member",
  "authenticated": true
}

// Response (not authenticated)
{
  "message": "Not authenticated"
} // Status: 401
```

**Logout**
```javascript
POST /auth/logout

// Response
{
  "message": "Logged out successfully"
}
// Clears authentication cookies
```

## Frontend Implementation Guide

### 1. Authentication State Management
```javascript
class AuthManager {
  constructor() {
    this.isAuthenticated = false;
    this.currentUser = null;
  }

  async checkAuth() {
    try {
      const response = await fetch('/api/auth/user', {
        credentials: 'include' // Important: include cookies
      });
      
      if (response.ok) {
        this.currentUser = await response.json();
        this.isAuthenticated = true;
        return true;
      } else {
        this.isAuthenticated = false;
        return false;
      }
    } catch (error) {
      console.error('Auth check failed:', error);
      return false;
    }
  }

  async sendSmsCode(phoneNumber) {
    const response = await fetch('/api/auth/sms/send', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber }),
      credentials: 'include'
    });
    return await response.json();
  }

  async verifySmsCode(phoneNumber, code) {
    const response = await fetch('/api/auth/sms/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber, code }),
      credentials: 'include'
    });
    
    if (response.ok) {
      await this.checkAuth(); // Refresh user state
    }
    
    return await response.json();
  }

  async sendMagicLink(email) {
    const response = await fetch('/api/auth/email/send', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email }),
      credentials: 'include'
    });
    return await response.json();
  }

  async authenticateMagicLink(token) {
    const response = await fetch('/api/auth/email/authenticate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token }),
      credentials: 'include'
    });
    
    if (response.ok) {
      await this.checkAuth(); // Refresh user state
    }
    
    return await response.json();
  }

  async logout() {
    const response = await fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'include'
    });
    
    if (response.ok) {
      this.isAuthenticated = false;
      this.currentUser = null;
    }
    
    return await response.json();
  }
}
```

### 2. Key Frontend Requirements

**Always Include Credentials**
```javascript
// CRITICAL: All requests must include credentials for cookie handling
fetch('/api/auth/endpoint', {
  credentials: 'include'  // This ensures cookies are sent/received
});
```

**Error Handling**
```javascript
async function handleAuthRequest(requestFn) {
  try {
    const result = await requestFn();
    
    if (!result.ok) {
      throw new Error(result.message || 'Authentication failed');
    }
    
    return result;
  } catch (error) {
    console.error('Auth error:', error);
    // Show user-friendly error message
    showError(error.message);
    throw error;
  }
}
```

**Route Protection**
```javascript
async function requireAuth() {
  const authManager = new AuthManager();
  const isAuthenticated = await authManager.checkAuth();
  
  if (!isAuthenticated) {
    // Redirect to login page
    window.location.href = '/login.html';
    return false;
  }
  
  return true;
}
```

### 3. Login Page Implementation
```html
<!-- SMS Login Form -->
<form id="smsLoginForm">
  <input type="tel" id="phoneNumber" placeholder="+1234567890" required>
  <button type="submit">Send Code</button>
</form>

<form id="smsVerifyForm" style="display:none">
  <input type="text" id="smsCode" placeholder="Enter 6-digit code" required>
  <button type="submit">Verify</button>
</form>

<!-- Email Login Form -->
<form id="emailLoginForm">
  <input type="email" id="email" placeholder="user@example.com" required>
  <button type="submit">Send Magic Link</button>
</form>

<script>
const authManager = new AuthManager();

// SMS Authentication
document.getElementById('smsLoginForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const phoneNumber = document.getElementById('phoneNumber').value;
  
  try {
    const result = await authManager.sendSmsCode(phoneNumber);
    if (result.ok) {
      document.getElementById('smsLoginForm').style.display = 'none';
      document.getElementById('smsVerifyForm').style.display = 'block';
    }
  } catch (error) {
    alert('Failed to send SMS: ' + error.message);
  }
});

document.getElementById('smsVerifyForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const phoneNumber = document.getElementById('phoneNumber').value;
  const code = document.getElementById('smsCode').value;
  
  try {
    const result = await authManager.verifySmsCode(phoneNumber, code);
    if (result.ok) {
      window.location.href = '/app.html'; // Redirect to main app
    }
  } catch (error) {
    alert('Verification failed: ' + error.message);
  }
});

// Email Authentication
document.getElementById('emailLoginForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  const email = document.getElementById('email').value;
  
  try {
    const result = await authManager.sendMagicLink(email);
    if (result.ok) {
      alert('Magic link sent! Check your email.');
    }
  } catch (error) {
    alert('Failed to send magic link: ' + error.message);
  }
});

// Check authentication status on page load
document.addEventListener('DOMContentLoaded', async () => {
  const isAuthenticated = await authManager.checkAuth();
  if (isAuthenticated) {
    window.location.href = '/app.html';
  }
});
</script>
```

### 4. Magic Link Callback Page Implementation
```html
<!-- auth/callback.html -->
<script>
document.addEventListener('DOMContentLoaded', async () => {
  const urlParams = new URLSearchParams(window.location.search);
  const token = urlParams.get('token');
  
  if (token) {
    const authManager = new AuthManager();
    try {
      const result = await authManager.authenticateMagicLink(token);
      if (result.ok) {
        window.location.href = '/app.html';
      } else {
        alert('Authentication failed: ' + result.message);
        window.location.href = '/login.html';
      }
    } catch (error) {
      alert('Authentication error: ' + error.message);
      window.location.href = '/login.html';
    }
  } else {
    alert('No authentication token found');
    window.location.href = '/login.html';
  }
});
</script>
```

## Important Notes

1. **Cookie-Based Sessions**: Authentication uses httpOnly cookies (`ssid`), so no manual token management required
2. **CORS Configuration**: Ensure your Azure Functions app allows credentials from your frontend domain
3. **HTTPS Required**: Secure cookies require HTTPS in production
4. **Error Handling**: Always check the `ok` field in responses before proceeding
5. **State Management**: Call `/auth/user` on page load to check authentication status
6. **Legacy Stytch Session Support**: The `/auth/session` endpoint can still be used to exchange existing Stytch session JWTs for backend sessions during migration

## Migration from Direct Stytch Integration

This specification replaces all direct Stytch API calls with your backend endpoints, providing:
- **Enhanced Security**: Stytch credentials are server-side only
- **Better Error Handling**: Centralized error responses
- **Consistent Session Management**: Unified cookie-based authentication
- **Team Integration**: Automatic team metadata handling

Replace all `callStytchAPI()` calls in your existing frontend with the corresponding backend endpoint calls as specified above.
