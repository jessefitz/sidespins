# Stytch SDK to Direct API Migration

## Overview
This document outlines the migration from Stytch JavaScript SDK to direct API calls for the SideSpins authentication system.

## Changes Made

### 1. login.html
- **Removed**: Stytch SDK script and initialization
- **Added**: Direct API calling functionality with `callStytchAPI()` helper function
- **Updated**: SMS OTP authentication to use `/otps/sms/login_or_create` and `/otps/sms/authenticate` endpoints
- **Updated**: Magic link authentication to use `/magic_links/email/login_or_create` endpoint

### 2. auth/callback.html
- **Removed**: Stytch SDK script and initialization
- **Added**: Direct API calling functionality
- **Updated**: Magic link authentication callback to use `/magic_links/authenticate` endpoint

## API Endpoints Used

### SMS Authentication
1. **Send SMS OTP**: `POST /v1/otps/sms/login_or_create`
   - Body: `{ "phone_number": "+1234567890" }`
   
2. **Verify SMS OTP**: `POST /v1/otps/sms/authenticate`
   - Body: `{ "phone_number": "+1234567890", "code": "123456", "session_duration_minutes": 60 }`

### Magic Link Authentication
1. **Send Magic Link**: `POST /v1/magic_links/email/login_or_create`
   - Body: `{ "email": "user@example.com", "login_magic_link_url": "...", "signup_magic_link_url": "..." }`

2. **Authenticate Magic Link**: `POST /v1/magic_links/authenticate`
   - Body: `{ "token": "magic_link_token", "session_duration_minutes": 60 }`

## Configuration Required

### Important: Update API Configuration
You need to update the following configuration values in both files:

```javascript
const STYTCH_PROJECT_ID = 'project-test-a2f77b99-578b-4bb0-986f-7b0404a5bf4b'; // Replace with your actual project ID
const STYTCH_PUBLIC_TOKEN = 'public-token-test-a2f77b99-578b-4bb0-986f-7b0404a5bf4b'; // Your public token
const STYTCH_API_BASE = 'https://test.stytch.com/v1'; // Use 'https://api.stytch.com/v1' for production
```

### Environment-Specific URLs
- **Test Environment**: `https://test.stytch.com/v1`
- **Production Environment**: `https://api.stytch.com/v1`

## Authentication Method
The implementation uses HTTP Basic Authentication with:
- **Username**: Your Stytch Project ID
- **Password**: Your Stytch Public Token

## Error Handling
Enhanced error handling now includes:
- Specific error messages from Stytch API responses
- Proper logging of API responses for debugging
- Fallback error messages for network issues

## Benefits of Direct API Calls
1. **Reduced Bundle Size**: No need to load the Stytch SDK
2. **Better Control**: Direct control over API requests and responses
3. **Debugging**: Easier to debug and monitor API calls
4. **Customization**: Easier to customize request/response handling

## Testing
After updating the configuration:
1. Test SMS OTP flow
2. Test Magic Link flow
3. Verify error handling scenarios
4. Check browser console for any API errors

## Security Notes
- The public token is safe to use in client-side code
- All sensitive operations still require backend validation
- Session exchange with your backend remains unchanged
