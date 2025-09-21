// SideSpins API Configuration and Utilities
// This module provides centralized API configuration and common utilities
// for interacting with the Azure Functions backend

class SideSpinsAPI {
  constructor() {
    // Determine API base URL based on environment
    this.baseURL = this.getAPIBaseURL();
    this.apiSecret = this.getAPISecret();
    
    // Default headers for all requests
    this.defaultHeaders = {
      'Content-Type': 'application/json',
      'x-api-secret': this.apiSecret
    };

    // Request timeout (30 seconds)
    this.timeout = 30000;
    
    // Cache for frequently accessed data
    this.cache = new Map();
    this.cacheTimeout = 5 * 60 * 1000; // 5 minutes
  }

  // Determine API base URL based on environment
  getAPIBaseURL() {
    const hostname = window.location.hostname;
    
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return 'http://localhost:7071';
    } else if (hostname.includes('github.io') || hostname === 'sidespins.com') {
      return 'https://sidespins.azurewebsites.net';
    } else {
      // Default to production for other domains
      return 'https://sidespins.azurewebsites.net';
    }
  }

  // Get API secret (in production, this should come from a secure source)
  getAPISecret() {
    // For development, we can use a default value
    // In production, this should be loaded from environment or secure storage
    return 'your-api-secret-here'; // TODO: Move to secure configuration
  }

  // Generic fetch wrapper with error handling and timeouts
  async fetchWithTimeout(url, options = {}) {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);

    try {
      const response = await fetch(url, {
        ...options,
        signal: controller.signal,
        headers: {
          ...this.defaultHeaders,
          ...options.headers
        }
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const errorText = await response.text();
        throw new APIError(`HTTP ${response.status}: ${response.statusText}`, response.status, errorText);
      }

      return response;
    } catch (error) {
      clearTimeout(timeoutId);
      
      if (error.name === 'AbortError') {
        throw new APIError('Request timeout', 408, 'The request took too long to complete');
      }
      
      throw error;
    }
  }

  // GET request helper
  async get(endpoint, params = {}) {
    const url = new URL(`${this.baseURL}${endpoint}`);
    
    // Add query parameters
    Object.keys(params).forEach(key => {
      if (params[key] !== null && params[key] !== undefined) {
        url.searchParams.append(key, params[key]);
      }
    });

    const cacheKey = url.toString();
    
    // Check cache first
    if (this.cache.has(cacheKey)) {
      const cached = this.cache.get(cacheKey);
      if (Date.now() - cached.timestamp < this.cacheTimeout) {
        return cached.data;
      }
    }

    const response = await this.fetchWithTimeout(url.toString());
    const data = await response.json();
    
    // Cache successful responses
    this.cache.set(cacheKey, {
      data,
      timestamp: Date.now()
    });

    return data;
  }

  // POST request helper
  async post(endpoint, data = {}) {
    const response = await this.fetchWithTimeout(`${this.baseURL}${endpoint}`, {
      method: 'POST',
      body: JSON.stringify(data)
    });

    return response.json();
  }

  // PUT request helper
  async put(endpoint, data = {}) {
    const response = await this.fetchWithTimeout(`${this.baseURL}${endpoint}`, {
      method: 'PUT',
      body: JSON.stringify(data)
    });

    return response.json();
  }

  // DELETE request helper
  async delete(endpoint) {
    const response = await this.fetchWithTimeout(`${this.baseURL}${endpoint}`, {
      method: 'DELETE'
    });

    // DELETE might return empty response
    const text = await response.text();
    return text ? JSON.parse(text) : {};
  }

  // Clear cache
  clearCache() {
    this.cache.clear();
  }

  // Clear specific cache entry
  clearCacheEntry(endpoint, params = {}) {
    const url = new URL(`${this.baseURL}${endpoint}`);
    Object.keys(params).forEach(key => {
      if (params[key] !== null && params[key] !== undefined) {
        url.searchParams.append(key, params[key]);
      }
    });
    this.cache.delete(url.toString());
  }
}

// Custom error class for API errors
class APIError extends Error {
  constructor(message, status, details) {
    super(message);
    this.name = 'APIError';
    this.status = status;
    this.details = details;
  }
}

// Match Management API Methods
class MatchAPI extends SideSpinsAPI {
  // Team Match methods
  async getTeamMatch(teamMatchId, divisionId) {
    return this.get(`/api/team-matches/${teamMatchId}`, { divisionId });
  }

  async getPlayerMatchesByTeamMatch(teamMatchId, divisionId) {
    return this.get(`/api/team-matches/${teamMatchId}/player-matches`, { divisionId });
  }

  // Player Match methods
  async getPlayerMatch(playerMatchId, divisionId) {
    return this.get(`/api/player-matches/${playerMatchId}`, { divisionId });
  }

  async createPlayerMatch(teamMatchId, playerMatchData) {
    this.clearCache(); // Clear cache after mutations
    return this.post(`/api/team-matches/${teamMatchId}/player-matches`, playerMatchData);
  }

  async updatePlayerMatch(playerMatchId, playerMatchData) {
    this.clearCache(); // Clear cache after mutations
    return this.put(`/api/player-matches/${playerMatchId}`, playerMatchData);
  }

  async deletePlayerMatch(playerMatchId, divisionId) {
    this.clearCache(); // Clear cache after mutations
    return this.delete(`/api/player-matches/${playerMatchId}?divisionId=${divisionId}`);
  }

  // Game methods
  async getGamesByPlayerMatch(playerMatchId, divisionId) {
    return this.get(`/api/player-matches/${playerMatchId}/games`, { divisionId });
  }

  async recordGame(playerMatchId, gameData) {
    this.clearCache(); // Clear cache after mutations
    return this.post(`/api/player-matches/${playerMatchId}/games`, gameData);
  }

  // Player methods
  async getPlayers(divisionId) {
    return this.get('/api/players', { divisionId });
  }

  async getPlayer(playerId, divisionId) {
    return this.get(`/api/players/${playerId}`, { divisionId });
  }
}

// Utility functions for common operations
class MatchUtils {
  // Format duration from minutes to human readable
  static formatDuration(duration) {
    if (!duration) return '-';
    
    if (typeof duration === 'number') {
      const hours = Math.floor(duration / 60);
      const minutes = duration % 60;
      return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
    }
    
    return duration.toString();
  }

  // Format date for datetime-local input
  static formatDateTimeForInput(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toISOString().slice(0, 16);
  }

  // Get match status CSS class
  static getStatusClass(status) {
    if (!status) return 'status-pending';
    return `status-${status.toLowerCase().replace(/\s+/g, '-')}`;
  }

  // Get winner indicator class
  static getWinnerClass(winner) {
    return winner === 'home' ? 'winner-home' : 'winner-away';
  }

  // Validate player match data
  static validatePlayerMatch(data) {
    const errors = [];

    if (!data.homePlayerId) {
      errors.push('Home player is required');
    }

    if (!data.awayPlayerId) {
      errors.push('Away player is required');
    }

    if (data.homePlayerId === data.awayPlayerId) {
      errors.push('Home and away players must be different');
    }

    if (data.homePlayerScore < 0 || data.awayPlayerScore < 0) {
      errors.push('Scores cannot be negative');
    }

    return errors;
  }

  // Validate game data
  static validateGame(data) {
    const errors = [];

    if (!data.winner) {
      errors.push('Game winner is required');
    }

    if (!['home', 'away'].includes(data.winner)) {
      errors.push('Winner must be either "home" or "away"');
    }

    if (data.pointsHome < 0 || data.pointsAway < 0) {
      errors.push('Points cannot be negative');
    }

    if (!data.rackNumber || data.rackNumber <= 0) {
      errors.push('Valid rack number is required');
    }

    return errors;
  }

  // Calculate next rack number from games
  static getNextRackNumber(games) {
    if (!games || games.length === 0) {
      return 1;
    }
    
    const maxRack = Math.max(...games.map(g => g.rackNumber || 0));
    return maxRack + 1;
  }

  // Sort games by rack number
  static sortGamesByRack(games, descending = false) {
    return [...games].sort((a, b) => {
      const rackA = a.rackNumber || 0;
      const rackB = b.rackNumber || 0;
      return descending ? rackB - rackA : rackA - rackB;
    });
  }
}

// Alert system for user notifications
class AlertSystem {
  constructor(containerId = 'alertContainer') {
    this.container = document.getElementById(containerId);
    if (!this.container) {
      console.warn(`Alert container with ID '${containerId}' not found`);
    }
  }

  show(message, type = 'info', autoHide = true) {
    if (!this.container) return;

    const alert = document.createElement('div');
    alert.className = `alert alert-${type}`;
    alert.textContent = message;
    
    this.container.appendChild(alert);

    if (autoHide) {
      setTimeout(() => {
        if (alert.parentNode) {
          alert.parentNode.removeChild(alert);
        }
      }, 5000);
    }

    return alert;
  }

  showError(message, autoHide = true) {
    return this.show(message, 'error', autoHide);
  }

  showSuccess(message, autoHide = true) {
    return this.show(message, 'success', autoHide);
  }

  showWarning(message, autoHide = true) {
    return this.show(message, 'warning', autoHide);
  }

  showInfo(message, autoHide = true) {
    return this.show(message, 'info', autoHide);
  }

  clear() {
    if (this.container) {
      this.container.innerHTML = '';
    }
  }
}

// Loading state management
class LoadingManager {
  constructor() {
    this.loadingStates = new Map();
  }

  show(elementId, text = 'Loading...') {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Store original content
    if (!this.loadingStates.has(elementId)) {
      this.loadingStates.set(elementId, {
        originalContent: element.innerHTML,
        originalDisabled: element.disabled
      });
    }

    // Show loading state
    if (element.tagName === 'BUTTON') {
      element.disabled = true;
      element.textContent = text;
    } else {
      element.innerHTML = `<div class="loading">${text}</div>`;
    }
  }

  hide(elementId) {
    const element = document.getElementById(elementId);
    if (!element || !this.loadingStates.has(elementId)) return;

    const originalState = this.loadingStates.get(elementId);
    
    // Restore original state
    if (element.tagName === 'BUTTON') {
      element.disabled = originalState.originalDisabled;
      element.innerHTML = originalState.originalContent;
    } else {
      element.innerHTML = originalState.originalContent;
    }

    this.loadingStates.delete(elementId);
  }

  isLoading(elementId) {
    return this.loadingStates.has(elementId);
  }
}

// Global instances
window.matchAPI = new MatchAPI();
window.matchUtils = MatchUtils;
window.alertSystem = new AlertSystem();
window.loadingManager = new LoadingManager();

// Export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    MatchAPI,
    MatchUtils,
    AlertSystem,
    LoadingManager,
    APIError
  };
}