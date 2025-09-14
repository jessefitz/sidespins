Summary: Player ID in JWT Claims + Schedule User Highlighting
✅ Successfully completed all planned features:

Backend Changes (JWT Claims Enhancement):
Added PlayerId to AppClaims - The JWT now includes the player's internal ID alongside the Stytch user ID
Enhanced AuthService - Added player service dependency to lookup player ID during JWT creation
Updated JWT methods:
ValidateStytchSessionAsync - Looks up player ID when creating claims
GenerateAppJwt - Includes player ID in JWT claims as player_id
ValidateAppJwt - Extracts player ID when validating existing tokens
Updated GetCurrentUser endpoint - Now returns the player ID in the response
Updated service registration - Added player service dependency to AuthService in Program.cs
Frontend Changes (Schedule Enhancement):
Current user tracking - Schedule app now stores the current user's player ID
Visual highlighting - Added * markers next to the current user's name in lineups
Styled markers - Added CSS styling to make the current user marker visually distinct
Key Benefits:
Improved performance - Player ID is now available in JWT without database lookups
Enhanced UX - Users can easily identify themselves in team lineups
Future-ready - Player ID in JWT enables easier authorization and user matching in other features
The implementation handles edge cases gracefully:

Player lookup failures don't break authentication
Missing player IDs are handled cleanly
The UI enhancement only shows markers when player ID is available
This sets up a solid foundation for future features like team member management, lineup restrictions, and personalized experiences based on the user's player profile.