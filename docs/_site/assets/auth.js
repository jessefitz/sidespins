/**
 * SideSpins Authentication Manager
 * Handles all authentication operations using the backend API
 */
class AuthManager {
    constructor(baseUrl = 'http://localhost:7071/api') {
        this.baseUrl = baseUrl;
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
        try {
            const response = await fetch(`${this.baseUrl}/auth/user`, {
                credentials: 'include'
            });
            
            if (response.ok) {
                this.currentUser = await response.json();
                this.isAuthenticated = this.currentUser.authenticated || false;
                return this.isAuthenticated;
            } else {
                this.isAuthenticated = false;
                this.currentUser = null;
                return false;
            }
        } catch (error) {
            console.error('Auth check failed:', error);
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
     * Logout current user
     * @returns {Promise<Object>} API response
     */
    async logout() {
        try {
            const response = await fetch(`${this.baseUrl}/auth/logout`, {
                method: 'POST',
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (response.ok) {
                this.isAuthenticated = false;
                this.currentUser = null;
                this.userMemberships = null;
                this.activeTeamId = null;
                localStorage.removeItem('activeTeamId');
            }
            
            return result;
        } catch (error) {
            console.error('Logout failed:', error);
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
     * Require authentication - redirect to login if not authenticated
     * @param {string} loginUrl - URL to redirect to if not authenticated
     * @returns {Promise<boolean>} True if authenticated
     */
    async requireAuth(loginUrl = '/login.html') {
        const isAuthenticated = await this.checkAuth();
        
        if (!isAuthenticated) {
            window.location.href = loginUrl;
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
        return this.hasMinimumRole('admin');
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

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { AuthManager, UIPermissions };
} else if (typeof window !== 'undefined') {
    window.AuthManager = AuthManager;
    window.UIPermissions = UIPermissions;
}
