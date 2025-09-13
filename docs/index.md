---
layout: default
title: Home
---

# SideSpins - APA Pool League Management

<div id="loading" style="text-align: center; padding: 2rem;">
    <p>Checking authentication...</p>
</div>

<script src="/assets/auth.js"></script>
<script>
document.addEventListener('DOMContentLoaded', async function() {
    const authManager = new AuthManager();
    
    try {
        const isAuthenticated = await authManager.checkAuth();
        
        if (isAuthenticated) {
            // Redirect authenticated users to the dashboard
            window.location.href = '/app.html';
        } else {
            // Redirect unauthenticated users to login page
            window.location.href = '/login.html';
        }
    } catch (error) {
        console.error('Error checking authentication:', error);
        // Fallback to redirecting to login if auth check fails
        window.location.href = '/login.html';
    }
});
</script>
