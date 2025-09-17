/**
 * SideSpins Authentication Manager
 * Handles all authentication operations using the backend API
 */
class AuthManager {
    constructor(baseUrl = null) {
        this.baseUrl = baseUrl || (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1' 
            ? 'http://localhost:7071/api' 
            : 'https://api.sidespins.com/api');
        this.isAuthenticated = false;
        this.currentUser = null;
        this.currentPhoneId = null; // Store phone ID for SMS verification
        this.userMemberships = null; // Cache for team memberships
        this.activeTeamId = null; // Currently selected team
    }

    /**
     * Check current authentication status
     * @returns {Promise<boolean>} True if authenticated
     */
    async checkAuth() {
        const isIOSSafari = /iPad|iPhone|iPod/.test(navigator.userAgent) && /Safari/.test(navigator.userAgent) && !/Chrome/.test(navigator.userAgent);
        
        try {
            console.log('Checking authentication status...');
            console.log('Is iOS Safari:', isIOSSafari);
            
            const response = await fetch(`${this.baseUrl}/auth/user`, {
                credentials: 'include',
                // For iOS Safari, ensure we don't cache the request
                cache: 'no-cache'
            });
            
            console.log('Auth check response status:', response.status);
            console.log('Auth check response ok:', response.ok);
            
            if (response.ok) {
                this.currentUser = await response.json();
                this.isAuthenticated = this.currentUser.authenticated || false;
                console.log('Authentication status:', this.isAuthenticated);
                console.log('Current user:', this.currentUser);
                return this.isAuthenticated;
            } else {
                console.log('Auth check failed - response not ok');
                this.isAuthenticated = false;
                this.currentUser = null;
                return false;
            }
        } catch (error) {
            console.error('Auth check failed with error:', error);
            this.isAuthenticated = false;
            this.currentUser = null;
            return false;
        }
    }

    /**
     * Initialize new user signup with APA number and phone
     * @param {string} apaNumber - APA member number
     * @param {string} phoneNumber - Phone number in E.164 format
     * @returns {Promise<Object>} API response with profile and memberships
     */
    async signupInit(apaNumber, phoneNumber) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/signup/init`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ apaNumber, phoneNumber }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (result.success && result.memberships) {
                // Cache the memberships for later use
                this.userMemberships = result.memberships;
                
                // Set default active team (first team or no team)
                if (result.memberships.length > 0) {
                    this.activeTeamId = result.memberships[0].teamId;
                }
                
                // Store the phoneId for later verification, just like sendSmsCode does
                if (result.phoneId) {
                    this.currentPhoneId = result.phoneId;
                }
            }
            
            return result;
        } catch (error) {
            console.error('Signup init failed:', error);
            throw new Error('Failed to initialize signup');
        }
    }

    /**
     * Send SMS verification code
     * @param {string} phoneNumber - Phone number in E.164 format
     * @returns {Promise<Object>} API response with phoneId
     */
    async sendSmsCode(phoneNumber) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/sms/send`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneNumber }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            // Store the phoneId for later verification
            if (result.ok && result.phoneId) {
                this.currentPhoneId = result.phoneId;
            }
            
            return result;
        } catch (error) {
            console.error('SMS send failed:', error);
            throw new Error('Failed to send SMS code');
        }
    }

    /**
     * Verify SMS code
     * @param {string} phoneNumber - Phone number used for verification
     * @param {string} code - 6-digit verification code
     * @returns {Promise<Object>} API response
     */
    async verifySmsCode(phoneNumber, code) {
        try {
            if (!this.currentPhoneId) {
                throw new Error('No phone ID available. Please send SMS code first.');
            }
            
            const response = await fetch(`${this.baseUrl}/auth/sms/verify`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneId: this.currentPhoneId, code }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (result.ok) {
                await this.checkAuth(); // Refresh user state
                await this.loadUserMemberships(); // Load team memberships
                this.currentPhoneId = null; // Clear phone ID after successful verification
            }
            
            return result;
        } catch (error) {
            console.error('SMS verify failed:', error);
            throw new Error('Failed to verify SMS code');
        }
    }

    /**
     * Load user's team memberships
     * @returns {Promise<Array>} Array of team memberships
     */
    async loadUserMemberships() {
        try {
            const response = await fetch(`${this.baseUrl}/me/memberships`, {
                credentials: 'include'
            });
            
            if (response.ok) {
                const result = await response.json();
                this.userMemberships = result.memberships || [];
                
                // Set active team from localStorage or first available team
                const savedTeamId = localStorage.getItem('activeTeamId');
                if (savedTeamId && this.userMemberships.some(m => m.teamId === savedTeamId)) {
                    this.activeTeamId = savedTeamId;
                } else if (this.userMemberships.length > 0) {
                    this.activeTeamId = this.userMemberships[0].teamId;
                    localStorage.setItem('activeTeamId', this.activeTeamId);
                }
                
                return this.userMemberships;
            } else {
                this.userMemberships = [];
                return [];
            }
        } catch (error) {
            console.error('Failed to load memberships:', error);
            this.userMemberships = [];
            return [];
        }
    }

    /**
     * Get user profile with memberships
     * @returns {Promise<Object>} User profile data
     */
    async getUserProfile() {
        try {
            const response = await fetch(`${this.baseUrl}/me/profile`, {
                credentials: 'include'
            });
            
            if (response.ok) {
                return await response.json();
            } else {
                throw new Error('Failed to load profile');
            }
        } catch (error) {
            console.error('Failed to load profile:', error);
            throw error;
        }
    }

    /**
     * Set the active team for the current session
     * @param {string} teamId - Team ID to set as active
     */
    setActiveTeam(teamId) {
        if (this.userMemberships && this.userMemberships.some(m => m.teamId === teamId)) {
            this.activeTeamId = teamId;
            localStorage.setItem('activeTeamId', teamId);
            
            // Dispatch event for UI components to listen to
            window.dispatchEvent(new CustomEvent('teamChanged', { 
                detail: { teamId, membership: this.getActiveMembership() }
            }));
        } else {
            console.warn('Attempted to set invalid team ID:', teamId);
        }
    }

    /**
     * Get the current active team membership
     * @returns {Object|null} Active team membership or null
     */
    getActiveMembership() {
        if (!this.activeTeamId || !this.userMemberships) {
            return null;
        }
        
        return this.userMemberships.find(m => m.teamId === this.activeTeamId) || null;
    }

    /**
     * Check if user has minimum role for active team
     * @param {string} minimumRole - Minimum role required (player, captain, admin)
     * @returns {boolean} True if user has sufficient role
     */
    hasTeamRole(minimumRole) {
        const membership = this.getActiveMembership();
        if (!membership) return false;
        
        const roleRanks = { player: 1, captain: 2, manager: 2, admin: 3 };
        const userRank = roleRanks[membership.role.toLowerCase()] || 0;
        const requiredRank = roleRanks[minimumRole.toLowerCase()] || 999;
        
        return userRank >= requiredRank;
    }

    /**
     * Get the current team role for the active team
     * @returns {string} Role name or 'none' if no active team
     */
    getCurrentTeamRole() {
        const membership = this.getActiveMembership();
        return membership?.role || 'none';
    }

    /**
     * Handle API responses with enhanced error handling
     * @param {Response} response - Fetch response object
     * @returns {Promise<boolean>} True if error was handled
     */
    async handleApiError(response) {
        if (response.status === 403) {
            try {
                const errorData = await response.json();
                this.showAuthorizationErrorModal(errorData);
                return true;
            } catch (e) {
                this.showGenericAuthError();
                return true;
            }
        } else if (response.status === 401) {
            // Redirect to login
            window.location.href = '/login.html';
            return true;
        }
        return false;
    }

    /**
     * Show detailed authorization error modal
     * @param {Object} errorInfo - Error details from API
     */
    showAuthorizationErrorModal(errorInfo) {
        // Remove any existing modal
        const existingModal = document.querySelector('.auth-error-modal');
        if (existingModal) {
            existingModal.remove();
        }

        const modal = document.createElement('div');
        modal.className = 'auth-error-modal';
        modal.innerHTML = `
            <div class="modal-overlay">
                <div class="modal-content">
                    <h3>Permission Required</h3>
                    <p>${errorInfo.message || 'You don\'t have permission for this action'}</p>
                    ${errorInfo.suggestedAction ? `<p class="suggestion"><strong>Suggestion:</strong> ${errorInfo.suggestedAction}</p>` : ''}
                    ${errorInfo.availableActions && errorInfo.availableActions.length > 0 ? `
                        <div class="available-actions">
                            <h4>Available Actions:</h4>
                            <ul>
                                ${errorInfo.availableActions.map(action => `<li>${this.formatActionName(action)}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                    <button onclick="this.closest('.auth-error-modal').remove()">OK</button>
                </div>
            </div>
        `;
        
        // Add modal styles if not already present
        if (!document.querySelector('#auth-modal-styles')) {
            const styles = document.createElement('style');
            styles.id = 'auth-modal-styles';
            styles.textContent = `
                .auth-error-modal {
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    z-index: 10000;
                }
                .modal-overlay {
                    background: rgba(0,0,0,0.5);
                    width: 100%;
                    height: 100%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }
                .modal-content {
                    background: white;
                    padding: 20px;
                    border-radius: 8px;
                    max-width: 400px;
                    margin: 20px;
                }
                .suggestion {
                    background: #f0f8ff;
                    padding: 10px;
                    border-left: 4px solid #007acc;
                    margin: 10px 0;
                }
                .available-actions {
                    margin: 15px 0;
                }
                .available-actions ul {
                    margin: 5px 0;
                    padding-left: 20px;
                }
            `;
            document.head.appendChild(styles);
        }
        
        document.body.appendChild(modal);
    }

    /**
     * Show generic auth error
     */
    showGenericAuthError() {
        alert('You don\'t have permission to perform this action.');
    }

    /**
     * Format action name for display
     * @param {string} action - Action name with underscores
     * @returns {string} Formatted action name
     */
    formatActionName(action) {
        return action.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase());
    }

    /**
     * Enhanced API fetch with automatic error handling
     * @param {string} url - API endpoint
     * @param {Object} options - Fetch options
     * @returns {Promise<Response>} Response object
     */
    async apiRequest(url, options = {}) {
        const response = await fetch(url, {
            credentials: 'include',
            ...options
        });

        if (!response.ok) {
            const handled = await this.handleApiError(response);
            if (handled) {
                throw new Error('Request failed with handled error');
            }
        }

        return response;
    }

    /**
     * Check if user is a global admin
     * @returns {boolean} True if user has global admin privileges
     */
    isGlobalAdmin() {
        return this.currentUser?.sidespinsRole === 'admin';
    }

    /**
     * Send magic link to email
     * @param {string} email - Email address
     * @returns {Promise<Object>} API response
     */
    async sendMagicLink(email) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/email/send`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email }),
                credentials: 'include'
            });
            return await response.json();
        } catch (error) {
            console.error('Magic link send failed:', error);
            throw new Error('Failed to send magic link');
        }
    }

    /**
     * Authenticate magic link token
     * @param {string} token - Magic link token
     * @returns {Promise<Object>} API response
     */
    async authenticateMagicLink(token) {
        try {
            const response = await fetch(`${this.baseUrl}/auth/email/authenticate`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (result.ok) {
                await this.checkAuth(); // Refresh user state
                await this.loadUserMemberships(); // Load team memberships
            }
            
            return result;
        } catch (error) {
            console.error('Magic link auth failed:', error);
            throw new Error('Failed to authenticate magic link');
        }
    }

    /**
     * Safe redirect method that works reliably across all browsers including iOS Safari
     * @param {string} url - URL to redirect to
     */
    safeRedirect(url) {
        // Convert relative URLs to absolute URLs to prevent iOS Safari issues
        let absoluteUrl = url;
        if (url.startsWith('/')) {
            absoluteUrl = window.location.origin + url;
        }
        
        // Use window.location.assign() which is more reliable on iOS Safari
        // than setting window.location.href directly
        try {
            window.location.assign(absoluteUrl);
        } catch (error) {
            // Fallback to setting href if assign fails
            window.location.href = absoluteUrl;
        }
    }

    /**
     * Logout current user
     * @returns {Promise<Object>} API response
     */
    async logout() {
        console.log('Starting logout process...');
        
        const isIOSSafari = /iPad|iPhone|iPod/.test(navigator.userAgent) && /Safari/.test(navigator.userAgent) && !/Chrome/.test(navigator.userAgent);
        
        if (isIOSSafari) {
            return this.logoutIOSSafari();
        }
        
        try {
            const headers = {
                'Content-Type': 'application/json'
            };
            
            console.log('Making logout request to:', `${this.baseUrl}/auth/logout`);
            
            const response = await fetch(`${this.baseUrl}/auth/logout`, {
                method: 'POST',
                credentials: 'include',
                headers: headers,
                cache: 'no-cache'
            });
            
            console.log('Logout response status:', response.status);
            console.log('Logout response ok:', response.ok);
            
            let result;
            try {
                result = await response.json();
                console.log('Logout response body:', result);
            } catch (jsonError) {
                console.warn('Failed to parse logout response as JSON:', jsonError);
                result = { success: response.ok, message: 'Logout request completed' };
            }
            
            // Clear local state
            console.log('Clearing local authentication state...');
            this.isAuthenticated = false;
            this.currentUser = null;
            this.userMemberships = null;
            this.activeTeamId = null;
            localStorage.removeItem('activeTeamId');
            
            return result;
        } catch (error) {
            console.error('Logout failed with error:', error);
            // Even if logout fails, clear local state
            this.isAuthenticated = false;
            this.currentUser = null;
            this.userMemberships = null;
            this.activeTeamId = null;
            localStorage.removeItem('activeTeamId');
            
            throw new Error('Failed to logout');
        }
    }

    /**
     * Special logout handling for iOS Safari
     * @returns {Promise<Object>} API response
     */
    async logoutIOSSafari() {
        console.log('Starting iOS Safari logout process...');
        
        // Method 1: Try the standard logout first
        try {
            console.log('Attempting standard logout for iOS Safari...');
            
            const response = await fetch(`${this.baseUrl}/auth/logout`, {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache'
                },
                cache: 'no-cache'
            });
            
            console.log('iOS Safari logout response status:', response.status);
            
            let result = { success: true, message: 'Logout completed' };
            if (response.ok) {
                try {
                    result = await response.json();
                } catch (jsonError) {
                    console.warn('Could not parse JSON response, but logout appears successful');
                }
            }
            
            // Always clear local state for iOS Safari
            this.clearAllAuthData();
            
            return result;
            
        } catch (error) {
            console.warn('Standard logout failed on iOS Safari, trying alternative method:', error);
            
            // Method 2: Try without waiting for response
            try {
                // Fire and forget request
                fetch(`${this.baseUrl}/auth/logout`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    cache: 'no-cache'
                }).catch(e => console.log('Fire-and-forget logout request completed'));
                
                // Clear local state immediately
                this.clearAllAuthData();
                
                return { success: true, message: 'iOS Safari logout completed' };
                
            } catch (altError) {
                console.error('All logout methods failed on iOS Safari:', altError);
                
                // Last resort: just clear local state
                this.clearAllAuthData();
                
                return { success: false, message: 'Local logout completed' };
            }
        }
    }

    /**
     * Clear all authentication-related data (iOS Safari compatible)
     */
    clearAllAuthData() {
        console.log('Clearing all authentication data...');
        
        // Clear instance variables
        this.isAuthenticated = false;
        this.currentUser = null;
        this.userMemberships = null;
        this.activeTeamId = null;
        
        // Clear localStorage
        try {
            localStorage.removeItem('activeTeamId');
            localStorage.clear(); // Clear everything to be safe
        } catch (e) {
            console.warn('Error clearing localStorage:', e);
        }
        
        // Clear sessionStorage
        try {
            sessionStorage.removeItem('userProfile');
            sessionStorage.removeItem('userMemberships');
            sessionStorage.removeItem('activeTeamId');
            sessionStorage.clear(); // Clear everything to be safe
        } catch (e) {
            console.warn('Error clearing sessionStorage:', e);
        }
        
        // Try to clear cookies (iOS Safari specific)
        try {
            // Get all cookies and expire them
            document.cookie.split(";").forEach(function(c) { 
                document.cookie = c.replace(/^ +/, "").replace(/=.*/, "=;expires=" + new Date().toUTCString() + ";path=/"); 
            });
            
            // Also try with domain
            const domain = window.location.hostname;
            document.cookie.split(";").forEach(function(c) { 
                document.cookie = c.replace(/^ +/, "").replace(/=.*/, "=;expires=" + new Date().toUTCString() + ";path=/;domain=" + domain); 
            });
        } catch (e) {
            console.warn('Error clearing cookies:', e);
        }
        
        console.log('Authentication data cleared');
    }

    /**
     * Require authentication - redirect to login if not authenticated
     * @param {string} loginUrl - URL to redirect to if not authenticated
     * @returns {Promise<boolean>} True if authenticated
     */
    async requireAuth(loginUrl = '/login.html') {
        const isAuthenticated = await this.checkAuth();
        
        if (!isAuthenticated) {
            this.safeRedirect(loginUrl);
            return false;
        }
        
        // Load memberships if authenticated
        if (this.isAuthenticated && !this.userMemberships) {
            await this.loadUserMemberships();
        }
        
        return true;
    }

    /**
     * Make API request with team context
     * @param {string} endpoint - API endpoint (relative to base URL)
     * @param {Object} options - Fetch options
     * @param {boolean} requireTeam - Whether active team is required
     * @returns {Promise<Response>} Fetch response
     */
    async apiRequest(endpoint, options = {}, requireTeam = false) {
        if (requireTeam && !this.activeTeamId) {
            throw new Error('No active team selected');
        }
        
        const url = endpoint.includes('://') ? endpoint : `${this.baseUrl}${endpoint}`;
        
        const defaultOptions = {
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        };
        
        const response = await fetch(url, { ...defaultOptions, ...options });
        
        // Handle 403 responses by clearing team context if needed
        if (response.status === 403 && this.activeTeamId) {
            console.warn('403 response received, user may have lost team access');
            // Optionally refresh memberships and switch to a valid team
            await this.loadUserMemberships();
        }
        
        return response;
    }

    /**
     * Format phone number for display
     * @param {string} phoneNumber - Raw phone number
     * @returns {string} Formatted phone number
     */
    formatPhoneNumber(phoneNumber) {
        const cleaned = phoneNumber.replace(/\D/g, '');
        
        if (cleaned.length >= 10) {
            let displayValue = cleaned;
            if (displayValue.startsWith('1') && displayValue.length === 11) {
                displayValue = displayValue.substring(1);
            }
            return displayValue.replace(/(\d{3})(\d{3})(\d{4})/, '($1) $2-$3');
        }
        
        return phoneNumber;
    }

    /**
     * Convert phone number to E.164 format
     * @param {string} phoneNumber - Raw phone number
     * @returns {string} E.164 formatted phone number
     */
    toE164(phoneNumber) {
        let cleaned = phoneNumber.replace(/\D/g, '');
        
        // Remove leading 1 if present and we have 11 digits
        if (cleaned.startsWith('1') && cleaned.length === 11) {
            cleaned = cleaned.substring(1);
        }
        
        // Should have exactly 10 digits now
        if (cleaned.length !== 10) {
            throw new Error('Invalid phone number format');
        }
        
        return `+1${cleaned}`;
    }

    /**
     * Make an authenticated API request
     * @param {string} url - API endpoint URL
     * @param {Object} options - Fetch options
     * @returns {Promise<Response>} Fetch response
     */
    async makeAuthenticatedRequest(url, options = {}) {
        const defaultOptions = {
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        };

        const mergedOptions = { ...defaultOptions, ...options };
        
        try {
            const response = await fetch(url, mergedOptions);
            
            // If unauthorized, clear auth state and redirect to login
            if (response.status === 401) {
                this.isAuthenticated = false;
                this.currentUser = null;
                this.userMemberships = null;
                this.activeTeamId = null;
                localStorage.removeItem('activeTeamId');
                window.location.href = '/login.html';
                throw new Error('Authentication required');
            }
            
            return response;
        } catch (error) {
            console.error('Authenticated request failed:', error);
            throw error;
        }
    }
}

/**
 * UI Permissions Management
 * Handles role-based UI element visibility and state
 */
class UIPermissions {
    static authManager = null;

    static setAuthManager(authManager) {
        this.authManager = authManager;
    }

    /**
     * Get current team role
     * @returns {string} Current role or 'none'
     */
    static getCurrentTeamRole() {
        return this.authManager?.getCurrentTeamRole() || 'none';
    }

    /**
     * Make an authenticated API request
     * @param {string} url - API endpoint URL
     * @param {Object} options - Fetch options
     * @returns {Promise<Response>} Fetch response
     */
    async makeAuthenticatedRequest(url, options = {}) {
        const defaultOptions = {
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        };

        const mergedOptions = { ...defaultOptions, ...options };

        try {
            const response = await fetch(url, mergedOptions);
            
            // If unauthorized, clear auth state and redirect to login
            if (response.status === 401) {
                this.isAuthenticated = false;
                this.currentUser = null;
                this.userMemberships = null;
                this.activeTeamId = null;
                window.location.href = '/login.html';
                throw new Error('Authentication required');
            }
            
            return response;
        } catch (error) {
            console.error('Authenticated request failed:', error);
            throw error;
        }
    }

    /**
     * Check if user has minimum role
     * @param {string} requiredRole - Required role
     * @returns {boolean} True if user has sufficient role
     */
    static hasMinimumRole(requiredRole) {
        if (!this.authManager) return false;
        return this.authManager.hasTeamRole(requiredRole);
    }

    // Specific permission checks
    static canManageLineup() {
        return this.hasMinimumRole('manager');
    }
    
    static canEditPlayers() {
        return this.hasMinimumRole('manager');
    }
    
    static canViewSchedule() {
        return this.hasMinimumRole('player');
    }

    static canManageTeam() {
        return this.hasMinimumRole('captain');
    }

    static canEditSchedule() {
        return this.hasMinimumRole('manager');
    }

    static canAddRemovePlayers() {
        return this.hasMinimumRole('admin');
    }

    static canChangePlayerRoles() {
        return this.hasMinimumRole('admin');
    }

    /**
     * Apply conditional UI based on role-based attributes
     */
    static applyConditionalUI() {
        // Apply to all elements with role-based attributes
        document.querySelectorAll('[data-requires-role]').forEach(element => {
            const requiredRole = element.getAttribute('data-requires-role');
            const hasPermission = this.hasMinimumRole(requiredRole);
            
            if (hasPermission) {
                element.style.display = '';
                element.removeAttribute('disabled');
                element.classList.remove('disabled');
            } else {
                if (element.hasAttribute('data-hide-if-unauthorized')) {
                    element.style.display = 'none';
                } else {
                    element.setAttribute('disabled', 'true');
                    element.classList.add('disabled');
                    element.title = `Requires ${requiredRole} permissions`;
                }
            }
        });

        // Update role indicators
        document.querySelectorAll('[data-role-indicator]').forEach(element => {
            element.textContent = this.getCurrentTeamRole();
        });

        // Update team-specific content
        const activeTeam = this.authManager?.getActiveMembership();
        document.querySelectorAll('[data-team-name]').forEach(element => {
            element.textContent = activeTeam?.teamName || 'No Team Selected';
        });

        // Handle navigation visibility
        this.updateNavigationVisibility();
    }

    /**
     * Update navigation visibility based on roles
     */
    static updateNavigationVisibility() {
        // Team Admin link should only show for captains and managers
        const teamAdminLinks = document.querySelectorAll('.nav-team-admin');
        const showTeamAdmin = this.canManageTeam();
        teamAdminLinks.forEach(link => {
            link.style.display = showTeamAdmin ? '' : 'none';
        });
    }

    /**
     * Setup team change listener
     */
    static setupTeamChangeListener() {
        window.addEventListener('teamChanged', () => {
            this.applyConditionalUI();
        });
    }
}

/**
 * Mobile Navigation Handler
 * Manages responsive hamburger navigation menu
 */
class MobileNavigation {
    constructor() {
        this.isOpen = false;
        this.overlay = null;
        this.menu = null;
        this.hamburgerButton = null;
        this.closeButton = null;
    }

    /**
     * Initialize mobile navigation
     */
    init() {
        this.setupElements();
        this.setupEventListeners();
        this.syncTeamSelectors();
    }

    /**
     * Setup DOM elements
     */
    setupElements() {
        this.hamburgerButton = document.getElementById('hamburger-menu');
        this.overlay = document.getElementById('mobile-nav-overlay');
        this.menu = document.getElementById('mobile-nav-menu');
        this.closeButton = document.getElementById('mobile-nav-close');
    }

    /**
     * Setup event listeners
     */
    setupEventListeners() {
        if (this.hamburgerButton) {
            this.hamburgerButton.addEventListener('click', () => this.open());
        }

        if (this.closeButton) {
            this.closeButton.addEventListener('click', () => this.close());
        }

        if (this.overlay) {
            this.overlay.addEventListener('click', (e) => {
                if (e.target === this.overlay) {
                    this.close();
                }
            });
        }

        // Handle escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.isOpen) {
                this.close();
            }
        });

        // Sync team selectors
        const desktopTeamSelect = document.getElementById('team-select');
        const mobileTeamSelect = document.getElementById('mobile-team-select');

        if (desktopTeamSelect && mobileTeamSelect) {
            desktopTeamSelect.addEventListener('change', (e) => {
                mobileTeamSelect.value = e.target.value;
            });

            mobileTeamSelect.addEventListener('change', (e) => {
                desktopTeamSelect.value = e.target.value;
                // Trigger change event on desktop selector
                const event = new Event('change', { bubbles: true });
                desktopTeamSelect.dispatchEvent(event);
            });
        }

        // Handle mobile logout
        const mobileLogoutBtn = document.getElementById('mobile-logout-btn');
        if (mobileLogoutBtn) {
            mobileLogoutBtn.addEventListener('click', () => {
                const desktopLogoutBtn = document.getElementById('logout-btn');
                if (desktopLogoutBtn) {
                    desktopLogoutBtn.click();
                }
            });
        }
    }

    /**
     * Open mobile navigation
     */
    open() {
        this.isOpen = true;
        if (this.overlay) this.overlay.classList.add('show');
        if (this.menu) this.menu.classList.add('show');
        document.body.style.overflow = 'hidden';
    }

    /**
     * Close mobile navigation
     */
    close() {
        this.isOpen = false;
        if (this.overlay) this.overlay.classList.remove('show');
        if (this.menu) this.menu.classList.remove('show');
        document.body.style.overflow = '';
    }

    /**
     * Sync team selector options
     */
    syncTeamSelectors() {
        const desktopSelect = document.getElementById('team-select');
        const mobileSelect = document.getElementById('mobile-team-select');

        if (desktopSelect && mobileSelect) {
            // Copy options from desktop to mobile
            mobileSelect.innerHTML = desktopSelect.innerHTML;
            mobileSelect.value = desktopSelect.value;
        }
    }

    /**
     * Update team options in both selectors
     * @param {Array} teams - Array of team objects
     * @param {string} selectedTeamId - Currently selected team ID
     */
    updateTeamOptions(teams, selectedTeamId = null) {
        const desktopSelect = document.getElementById('team-select');
        const mobileSelect = document.getElementById('mobile-team-select');

        const optionsHtml = teams.map(team => 
            `<option value="${team.teamId}" ${team.teamId === selectedTeamId ? 'selected' : ''}>
                ${team.teamName || team.teamId || 'Unknown Team'}
            </option>`
        ).join('');

        if (desktopSelect) {
            desktopSelect.innerHTML = '<option value="">Select a team...</option>' + optionsHtml;
        }
        if (mobileSelect) {
            mobileSelect.innerHTML = '<option value="">Select a team...</option>' + optionsHtml;
        }
    }
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { AuthManager, UIPermissions, MobileNavigation };
} else if (typeof window !== 'undefined') {
    window.AuthManager = AuthManager;
    window.UIPermissions = UIPermissions;
    window.MobileNavigation = MobileNavigation;
}
