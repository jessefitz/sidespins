/**
 * SideSpins Authentication Manager
 * Handles all authentication operations using the backend API
 */
class AuthManager {
    constructor(baseUrl = 'http://localhost:7071/api/auth') {
        this.baseUrl = baseUrl;
        this.isAuthenticated = false;
        this.currentUser = null;
        this.currentPhoneId = null; // Store phone ID for SMS verification
    }

    /**
     * Check current authentication status
     * @returns {Promise<boolean>} True if authenticated
     */
    async checkAuth() {
        try {
            const response = await fetch(`${this.baseUrl}/user`, {
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
     * Send SMS verification code
     * @param {string} phoneNumber - Phone number in E.164 format
     * @returns {Promise<Object>} API response with phoneId
     */
    async sendSmsCode(phoneNumber) {
        try {
            const response = await fetch(`${this.baseUrl}/sms/send`, {
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
     * @param {string} code - 6-digit verification code
     * @returns {Promise<Object>} API response
     */
    async verifySmsCode(code) {
        try {
            if (!this.currentPhoneId) {
                throw new Error('No phone ID available. Please send SMS code first.');
            }
            
            const response = await fetch(`${this.baseUrl}/sms/verify`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneId: this.currentPhoneId, code }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (result.ok) {
                await this.checkAuth(); // Refresh user state
                this.currentPhoneId = null; // Clear phone ID after successful verification
            }
            
            return result;
        } catch (error) {
            console.error('SMS verify failed:', error);
            throw new Error('Failed to verify SMS code');
        }
    }

    /**
     * Send magic link to email
     * @param {string} email - Email address
     * @returns {Promise<Object>} API response
     */
    async sendMagicLink(email) {
        try {
            const response = await fetch(`${this.baseUrl}/email/send`, {
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
            const response = await fetch(`${this.baseUrl}/email/authenticate`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ token }),
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (result.ok) {
                await this.checkAuth(); // Refresh user state
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
            const response = await fetch(`${this.baseUrl}/logout`, {
                method: 'POST',
                credentials: 'include'
            });
            
            const result = await response.json();
            
            if (response.ok) {
                this.isAuthenticated = false;
                this.currentUser = null;
            }
            
            return result;
        } catch (error) {
            console.error('Logout failed:', error);
            // Even if logout fails, clear local state
            this.isAuthenticated = false;
            this.currentUser = null;
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
        
        return true;
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
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AuthManager;
} else if (typeof window !== 'undefined') {
    window.AuthManager = AuthManager;
}
