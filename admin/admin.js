// Admin Panel JavaScript for SideSpins API Management
class SideSpinsAdmin {
    constructor() {
        this.apiSecret = '';
        this.apiBaseUrl = 'http://localhost:7071/api'; // Change this to your production URL
        this.authenticated = false;
    }

    // Authentication
    setApiSecret(secret) {
        this.apiSecret = secret;
        this.authenticated = !!secret;
    }

    // Generic API request method
    async apiRequest(endpoint, options = {}) {
        if (!this.authenticated) {
            throw new Error('Not authenticated');
        }

        const url = `${this.apiBaseUrl}${endpoint}`;
        const config = {
            headers: {
                'x-api-secret': this.apiSecret,
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        };

        const response = await fetch(url, config);
        
        if (response.status === 401) {
            throw new Error('Invalid API secret');
        }
        
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`HTTP ${response.status}: ${errorText || response.statusText}`);
        }

        if (response.status === 204) {
            return null; // No content
        }

        return response.json();
    }

    // Player API methods
    async getPlayers() {
        return this.apiRequest('/GetPlayers');
    }

    async createPlayer(player) {
        return this.apiRequest('/CreatePlayer', {
            method: 'POST',
            body: JSON.stringify({
                type: 'player',
                firstName: player.firstName,
                lastName: player.lastName,
                apaNumber: player.apaNumber || null,
                createdAt: new Date().toISOString()
            })
        });
    }

    async updatePlayer(id, player) {
        return this.apiRequest(`/players/${id}`, {
            method: 'PATCH',
            body: JSON.stringify({
                id: id,
                type: 'player',
                firstName: player.firstName,
                lastName: player.lastName,
                apaNumber: player.apaNumber || null
            })
        });
    }

    async deletePlayer(id) {
        return this.apiRequest(`/players/${id}`, {
            method: 'DELETE'
        });
    }

    // Membership API methods
    async getMemberships(teamId) {
        return this.apiRequest(`/GetMemberships?teamId=${encodeURIComponent(teamId)}`);
    }

    async createMembership(membership) {
        return this.apiRequest('/CreateMembership', {
            method: 'POST',
            body: JSON.stringify({
                type: 'membership',
                teamId: membership.teamId,
                divisionId: membership.divisionId,
                playerId: membership.playerId,
                role: membership.role,
                skillLevel_9b: membership.skillLevel ? parseInt(membership.skillLevel) : null,
                joinedAt: new Date().toISOString(),
                leftAt: null
            })
        });
    }

    async deleteMembership(id) {
        return this.apiRequest(`/memberships/${id}`, {
            method: 'DELETE'
        });
    }

    // Match API methods
    async getMatches(divisionId) {
        return this.apiRequest(`/GetMatches?divisionId=${encodeURIComponent(divisionId)}`);
    }

    async updateMatchLineup(matchId, lineupPlan) {
        return this.apiRequest(`/matches/${matchId}/lineup`, {
            method: 'PATCH',
            body: JSON.stringify(lineupPlan)
        });
    }
}

// Global instance
const adminApi = new SideSpinsAdmin();

// UI Helper functions
function showStatus(message, type = 'info') {
    const container = document.getElementById('status-messages');
    const statusDiv = document.createElement('div');
    statusDiv.className = `status-message status-${type}`;
    statusDiv.textContent = message;
    
    container.appendChild(statusDiv);
    
    // Auto-remove after 5 seconds
    setTimeout(() => {
        if (statusDiv.parentNode) {
            statusDiv.parentNode.removeChild(statusDiv);
        }
    }, 5000);
}

function clearForm(formId) {
    const form = document.getElementById(formId);
    if (form) {
        form.reset();
    }
}

// Authentication functions
async function authenticate() {
    const secretInput = document.getElementById('api-secret');
    const secret = secretInput.value.trim();
    
    if (!secret) {
        showStatus('Please enter an API secret', 'error');
        return;
    }
    
    adminApi.setApiSecret(secret);
    
    try {
        // Test the authentication by trying to get players
        await adminApi.getPlayers();
        
        // Success - hide auth section and show admin panel
        document.getElementById('auth-section').style.display = 'none';
        document.getElementById('admin-panel').style.display = 'block';
        document.getElementById('auth-status').innerHTML = '<div class="status-success">✅ Authenticated successfully</div>';
        
        showStatus('Authentication successful!', 'success');
        
        // Load initial data
        loadPlayers();
        
    } catch (error) {
        showStatus(`Authentication failed: ${error.message}`, 'error');
        adminApi.setApiSecret('');
    }
}

// Tab management
function showTab(tabName) {
    // Hide all tabs
    const tabs = document.querySelectorAll('.tab-content');
    tabs.forEach(tab => tab.classList.remove('active'));
    
    // Hide all tab buttons
    const buttons = document.querySelectorAll('.tab-button');
    buttons.forEach(button => button.classList.remove('active'));
    
    // Show selected tab
    document.getElementById(`${tabName}-tab`).classList.add('active');
    event.target.classList.add('active');
}

// Player management functions
async function loadPlayers() {
    try {
        const players = await adminApi.getPlayers();
        displayPlayers(players);
        showStatus(`Loaded ${players.length} players`, 'success');
    } catch (error) {
        showStatus(`Error loading players: ${error.message}`, 'error');
    }
}

function displayPlayers(players) {
    const container = document.getElementById('players-list');
    
    if (!players || players.length === 0) {
        container.innerHTML = '<p>No players found</p>';
        return;
    }
    
    container.innerHTML = players.map(player => `
        <div class="data-item">
            <div class="data-item-content">
                <strong>${escapeHtml(player.firstName)} ${escapeHtml(player.lastName)}</strong>
                ${player.apaNumber ? `<br><small>APA #${escapeHtml(player.apaNumber)}</small>` : ''}
                <br><small>ID: ${escapeHtml(player.id)}</small>
            </div>
            <div class="data-item-actions">
                <button class="btn-edit" onclick="editPlayer('${escapeHtml(player.id)}', '${escapeHtml(player.firstName)}', '${escapeHtml(player.lastName)}', '${escapeHtml(player.apaNumber || '')}')">Edit</button>
                <button class="btn-danger" onclick="confirmDeletePlayer('${escapeHtml(player.id)}', '${escapeHtml(player.firstName)} ${escapeHtml(player.lastName)}')">Delete</button>
            </div>
        </div>
    `).join('');
}

async function createPlayer(event) {
    event.preventDefault();
    
    const firstName = document.getElementById('player-first-name').value.trim();
    const lastName = document.getElementById('player-last-name').value.trim();
    const apaNumber = document.getElementById('player-apa-number').value.trim();
    
    if (!firstName || !lastName) {
        showStatus('First name and last name are required', 'error');
        return;
    }
    
    try {
        const player = await adminApi.createPlayer({
            firstName,
            lastName,
            apaNumber: apaNumber || null
        });
        
        showStatus(`Player "${firstName} ${lastName}" created successfully`, 'success');
        clearForm('player-form');
        loadPlayers(); // Refresh the list
    } catch (error) {
        showStatus(`Error creating player: ${error.message}`, 'error');
    }
}

function editPlayer(id, firstName, lastName, apaNumber) {
    // Fill the form with current values
    document.getElementById('player-first-name').value = firstName;
    document.getElementById('player-last-name').value = lastName;
    document.getElementById('player-apa-number').value = apaNumber;
    
    // Change the form to update mode
    const form = document.getElementById('player-form');
    form.onsubmit = (e) => updatePlayer(e, id);
    
    // Update button text
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Update Player';
    submitBtn.style.background = '#28a745';
    
    // Add cancel button
    if (!form.querySelector('.cancel-edit')) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.className = 'btn-secondary cancel-edit';
        cancelBtn.style.marginLeft = '0.5rem';
        cancelBtn.onclick = cancelEditPlayer;
        submitBtn.parentNode.appendChild(cancelBtn);
    }
}

async function updatePlayer(event, id) {
    event.preventDefault();
    
    const firstName = document.getElementById('player-first-name').value.trim();
    const lastName = document.getElementById('player-last-name').value.trim();
    const apaNumber = document.getElementById('player-apa-number').value.trim();
    
    if (!firstName || !lastName) {
        showStatus('First name and last name are required', 'error');
        return;
    }
    
    try {
        await adminApi.updatePlayer(id, {
            firstName,
            lastName,
            apaNumber: apaNumber || null
        });
        
        showStatus(`Player "${firstName} ${lastName}" updated successfully`, 'success');
        cancelEditPlayer(); // Reset form
        loadPlayers(); // Refresh the list
    } catch (error) {
        showStatus(`Error updating player: ${error.message}`, 'error');
    }
}

function cancelEditPlayer() {
    const form = document.getElementById('player-form');
    form.reset();
    form.onsubmit = createPlayer;
    
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Add Player';
    submitBtn.style.background = '#007bff';
    
    const cancelBtn = form.querySelector('.cancel-edit');
    if (cancelBtn) {
        cancelBtn.remove();
    }
}

function confirmDeletePlayer(id, name) {
    if (confirm(`Are you sure you want to delete player "${name}"?`)) {
        deletePlayer(id, name);
    }
}

async function deletePlayer(id, name) {
    try {
        await adminApi.deletePlayer(id);
        showStatus(`Player "${name}" deleted successfully`, 'success');
        loadPlayers(); // Refresh the list
    } catch (error) {
        showStatus(`Error deleting player: ${error.message}`, 'error');
    }
}

// Membership management functions
async function loadMemberships() {
    const teamId = document.getElementById('team-id').value.trim();
    
    if (!teamId) {
        showStatus('Please enter a team ID', 'error');
        return;
    }
    
    try {
        const memberships = await adminApi.getMemberships(teamId);
        displayMemberships(memberships);
        showStatus(`Loaded ${memberships.length} memberships for team ${teamId}`, 'success');
    } catch (error) {
        showStatus(`Error loading memberships: ${error.message}`, 'error');
    }
}

function displayMemberships(memberships) {
    const container = document.getElementById('memberships-list');
    
    if (!memberships || memberships.length === 0) {
        container.innerHTML = '<p>No memberships found</p>';
        return;
    }
    
    container.innerHTML = memberships.map(membership => `
        <div class="data-item">
            <div class="data-item-content">
                <strong>Player ID: ${escapeHtml(membership.playerId)}</strong>
                <br><small>Role: ${escapeHtml(membership.role)}</small>
                ${membership.skillLevel_9b ? `<br><small>Skill Level: ${membership.skillLevel_9b}</small>` : ''}
                <br><small>ID: ${escapeHtml(membership.id)}</small>
            </div>
            <div class="data-item-actions">
                <button class="btn-danger" onclick="confirmDeleteMembership('${escapeHtml(membership.id)}', '${escapeHtml(membership.playerId)}')">Delete</button>
            </div>
        </div>
    `).join('');
}

async function createMembership(event) {
    event.preventDefault();
    
    const teamId = document.getElementById('membership-team-id').value.trim();
    const divisionId = document.getElementById('membership-division-id').value.trim();
    const playerId = document.getElementById('membership-player-id').value.trim();
    const role = document.getElementById('membership-role').value.trim();
    const skillLevel = document.getElementById('membership-skill-level').value.trim();
    
    if (!teamId || !divisionId || !playerId || !role) {
        showStatus('Team ID, Division ID, Player ID, and Role are required', 'error');
        return;
    }
    
    try {
        const membership = await adminApi.createMembership({
            teamId,
            divisionId,
            playerId,
            role,
            skillLevel: skillLevel || null
        });
        
        showStatus(`Membership created successfully for player ${playerId}`, 'success');
        clearForm('membership-form');
        
        // Refresh if we're viewing this team
        const currentTeamId = document.getElementById('team-id').value.trim();
        if (currentTeamId === teamId) {
            loadMemberships();
        }
    } catch (error) {
        showStatus(`Error creating membership: ${error.message}`, 'error');
    }
}

function confirmDeleteMembership(id, playerId) {
    if (confirm(`Are you sure you want to delete the membership for player "${playerId}"?`)) {
        deleteMembership(id, playerId);
    }
}

async function deleteMembership(id, playerId) {
    try {
        await adminApi.deleteMembership(id);
        showStatus(`Membership for player "${playerId}" deleted successfully`, 'success');
        loadMemberships(); // Refresh the list
    } catch (error) {
        showStatus(`Error deleting membership: ${error.message}`, 'error');
    }
}

// Match management functions
async function loadMatches() {
    const divisionId = document.getElementById('division-id').value.trim();
    
    if (!divisionId) {
        showStatus('Please enter a division ID', 'error');
        return;
    }
    
    try {
        const matches = await adminApi.getMatches(divisionId);
        displayMatches(matches);
        showStatus(`Loaded ${matches.length} matches for division ${divisionId}`, 'success');
    } catch (error) {
        showStatus(`Error loading matches: ${error.message}`, 'error');
    }
}

function displayMatches(matches) {
    const container = document.getElementById('matches-list');
    
    if (!matches || matches.length === 0) {
        container.innerHTML = '<p>No matches found</p>';
        return;
    }
    
    container.innerHTML = matches.map(match => {
        const scheduledDate = new Date(match.scheduledAt).toLocaleDateString();
        const homeTeam = match.homeTeamId || 'TBD';
        const awayTeam = match.awayTeamId || 'TBD';
        
        return `
            <div class="match-item">
                <div class="match-header">
                    Week ${match.week}: ${escapeHtml(homeTeam)} vs ${escapeHtml(awayTeam)}
                </div>
                <div class="match-details">
                    <strong>Date:</strong> ${scheduledDate}<br>
                    <strong>Status:</strong> ${escapeHtml(match.status)}<br>
                    <strong>Match ID:</strong> ${escapeHtml(match.id)}
                </div>
                
                ${match.lineupPlan ? renderLineupPlan(match.lineupPlan, match.id) : ''}
            </div>
        `;
    }).join('');
}

function renderLineupPlan(lineup, matchId) {
    return `
        <div class="lineup-section">
            <h4>Lineup Plan</h4>
            <div class="match-details">
                <strong>Ruleset:</strong> ${escapeHtml(lineup.ruleset)}<br>
                <strong>Max Skill Cap:</strong> ${lineup.maxTeamSkillCap}<br>
                <strong>Locked:</strong> ${lineup.locked ? 'Yes' : 'No'}
            </div>
            
            ${lineup.home && lineup.home.length > 0 ? `
                <div class="lineup-team">
                    <h4>Home Team (Total: ${lineup.totals?.homePlannedSkillSum || 0})</h4>
                    <div class="lineup-players">
                        ${lineup.home.map(player => `
                            <span class="lineup-player ${player.isAlternate ? 'alternate' : ''}">
                                ${escapeHtml(player.playerId)} (${player.skillLevel})
                                ${player.isAlternate ? ' [ALT]' : ''}
                            </span>
                        `).join('')}
                    </div>
                </div>
            ` : ''}
            
            ${lineup.away && lineup.away.length > 0 ? `
                <div class="lineup-team">
                    <h4>Away Team (Total: ${lineup.totals?.awayPlannedSkillSum || 0})</h4>
                    <div class="lineup-players">
                        ${lineup.away.map(player => `
                            <span class="lineup-player ${player.isAlternate ? 'alternate' : ''}">
                                ${escapeHtml(player.playerId)} (${player.skillLevel})
                                ${player.isAlternate ? ' [ALT]' : ''}
                            </span>
                        `).join('')}
                    </div>
                </div>
            ` : ''}
        </div>
    `;
}

// Utility functions
function escapeHtml(text) {
    if (text == null) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Initialize the admin panel when the page loads
document.addEventListener('DOMContentLoaded', function() {
    // Set up Enter key for auth
    document.getElementById('api-secret').addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            authenticate();
        }
    });
    
    // Focus on the API secret input
    document.getElementById('api-secret').focus();
});
