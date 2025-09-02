// Admin Panel JavaScript for SideSpins API Management
class SideSpinsAdmin {
    constructor() {
        this.apiSecret = '';
        // Use environment-appropriate URL
        this.apiBaseUrl = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1' 
            ? 'http://localhost:7071/api' 
            : 'https://sidespinsapi.azurewebsites.net/api';
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
                skillLevel_9b: membership.skillLevel_9b,
                joinedAt: new Date().toISOString(),
                leftAt: null
            })
        });
    }

    async deleteMembership(id, teamId) {
        return this.apiRequest(`/memberships/${id}?teamId=${encodeURIComponent(teamId)}`, {
            method: 'DELETE'
        });
    }

    async updateMembership(id, teamId, membership) {
        return this.apiRequest(`/memberships/${id}`, {
            method: 'PUT',
            body: JSON.stringify({
                id: id,
                type: 'membership',
                teamId: membership.teamId,
                divisionId: membership.divisionId,
                playerId: membership.playerId,
                role: membership.role,
                skillLevel_9b: membership.skillLevel_9b,
                joinedAt: membership.joinedAt
            })
        });
    }

    // Match API methods
    async getMatches(divisionId, startDate = null, endDate = null) {
        let url = `/GetMatches?divisionId=${encodeURIComponent(divisionId)}`;
        if (startDate && endDate) {
            url += `&startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;
        }
        return this.apiRequest(url);
    }

    // Team API methods
    async getTeams(divisionId) {
        return this.apiRequest(`/GetTeams?divisionId=${encodeURIComponent(divisionId)}`);
    }

    async createTeam(team) {
        return this.apiRequest('/CreateTeam', {
            method: 'POST',
            body: JSON.stringify({
                type: 'team',
                divisionId: team.divisionId,
                name: team.name,
                captainPlayerId: team.captainPlayerId || null,
                createdAt: new Date().toISOString()
            })
        });
    }

    async updateTeam(teamId, divisionId, team) {
        return this.apiRequest(`/teams/${teamId}?divisionId=${encodeURIComponent(divisionId)}`, {
            method: 'PUT',
            body: JSON.stringify({
                id: teamId,
                type: 'team',
                divisionId: team.divisionId,
                name: team.name,
                captainPlayerId: team.captainPlayerId || null
            })
        });
    }

    async deleteTeam(teamId, divisionId) {
        return this.apiRequest(`/teams/${teamId}?divisionId=${encodeURIComponent(divisionId)}`, {
            method: 'DELETE'
        });
    }

    async getTeamMemberships(teamId) {
        return this.apiRequest(`/GetMemberships?teamId=${encodeURIComponent(teamId)}`);
    }

    async getPlayerDetails(playerId) {
        // Since we don't have individual player endpoint, fetch all players and find the one we need
        const players = await this.getPlayers();
        return players.find(p => p.id === playerId) || null;
    }

    async createMatch(match) {
        return this.apiRequest('/matches', {
            method: 'POST',
            body: JSON.stringify({
                type: 'teamMatch',
                divisionId: match.divisionId,
                week: parseInt(match.week),
                scheduledAt: match.scheduledAt,
                homeTeamId: match.homeTeamId || null,
                awayTeamId: match.awayTeamId || null,
                status: match.status || 'scheduled',
                lineupPlan: {
                    ruleset: match.ruleset || '9-ball',
                    maxTeamSkillCap: parseInt(match.maxTeamSkillCap) || 23,
                    home: [],
                    away: [],
                    totals: {
                        homePlannedSkillSum: 0,
                        awayPlannedSkillSum: 0,
                        homeWithinCap: true,
                        awayWithinCap: true
                    },
                    locked: false,
                    history: []
                },
                playerMatches: [],
                totals: {
                    homePoints: 0,
                    awayPoints: 0,
                    bonusPoints: {
                        home: 0,
                        away: 0
                    }
                },
                createdAt: new Date().toISOString()
            })
        });
    }

    async updateMatch(matchId, divisionId, match) {
        return this.apiRequest(`/matches/${matchId}?divisionId=${encodeURIComponent(divisionId)}`, {
            method: 'PUT',
            body: JSON.stringify(match)
        });
    }

    async deleteMatch(matchId, divisionId) {
        return this.apiRequest(`/matches/${matchId}?divisionId=${encodeURIComponent(divisionId)}`, {
            method: 'DELETE'
        });
    }

    async updateMatchLineup(matchId, divisionId, lineupPlan) {
        return this.apiRequest(`/matches/${matchId}/lineup?divisionId=${encodeURIComponent(divisionId)}`, {
            method: 'PATCH',
            body: JSON.stringify(lineupPlan)
        });
    }
}

// Global instance
const adminApi = new SideSpinsAdmin();

// Store current matches data globally for editing
let currentMatchesData = [];

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
        loadMemberships(); // Load memberships for the default team
        
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

// Team management functions
async function loadTeams() {
    const divisionId = document.getElementById('teams-division-id').value.trim();
    
    if (!divisionId) {
        showStatus('Please enter a division ID', 'error');
        return;
    }
    
    try {
        const teams = await adminApi.getTeams(divisionId);
        displayTeams(teams);
        showStatus(`Loaded ${teams.length} teams for division ${divisionId}`, 'success');
    } catch (error) {
        showStatus(`Error loading teams: ${error.message}`, 'error');
    }
}

function displayTeams(teams) {
    const container = document.getElementById('teams-list');
    
    if (!teams || teams.length === 0) {
        container.innerHTML = '<p>No teams found</p>';
        return;
    }
    
    container.innerHTML = teams.map(team => `
        <div class="data-item">
            <div class="data-item-content">
                <strong>${escapeHtml(team.name)}</strong>
                <br><small>ID: ${escapeHtml(team.id)}</small>
                ${team.captainPlayerId ? `<br><small>Captain: ${escapeHtml(team.captainPlayerId)}</small>` : ''}
                <br><small>Division: ${escapeHtml(team.divisionId)}</small>
            </div>
            <div class="data-item-actions">
                <button class="btn-edit" onclick="editTeam('${escapeHtml(team.id)}', '${escapeHtml(team.divisionId)}', '${escapeHtml(team.name)}', '${escapeHtml(team.captainPlayerId || '')}')">Edit</button>
                <button class="btn-danger" onclick="confirmDeleteTeam('${escapeHtml(team.id)}', '${escapeHtml(team.divisionId)}', '${escapeHtml(team.name)}')">Delete</button>
            </div>
        </div>
    `).join('');
}

async function createTeam(event) {
    event.preventDefault();
    
    const divisionId = document.getElementById('team-division-id').value.trim();
    const name = document.getElementById('team-name').value.trim();
    const captainPlayerId = document.getElementById('team-captain-player-id').value.trim();
    
    if (!divisionId || !name) {
        showStatus('Division ID and Team Name are required', 'error');
        return;
    }
    
    try {
        const team = await adminApi.createTeam({
            divisionId,
            name,
            captainPlayerId: captainPlayerId || null
        });
        
        showStatus(`Team "${name}" created successfully`, 'success');
        clearForm('team-form');
        loadTeams(); // Refresh the list
    } catch (error) {
        showStatus(`Error creating team: ${error.message}`, 'error');
    }
}

function editTeam(id, divisionId, name, captainPlayerId) {
    // Fill the form with current values
    document.getElementById('team-division-id').value = divisionId;
    document.getElementById('team-name').value = name;
    document.getElementById('team-captain-player-id').value = captainPlayerId;
    
    // Change the form to update mode
    const form = document.getElementById('team-form');
    form.onsubmit = (e) => updateTeam(e, id, divisionId);
    
    // Update button text
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Update Team';
    submitBtn.style.background = '#28a745';
    
    // Add cancel button
    if (!form.querySelector('.cancel-edit')) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.className = 'btn-secondary cancel-edit';
        cancelBtn.style.marginLeft = '0.5rem';
        cancelBtn.onclick = cancelEditTeam;
        submitBtn.parentNode.appendChild(cancelBtn);
    }
}

async function updateTeam(event, id, divisionId) {
    event.preventDefault();
    
    const newDivisionId = document.getElementById('team-division-id').value.trim();
    const name = document.getElementById('team-name').value.trim();
    const captainPlayerId = document.getElementById('team-captain-player-id').value.trim();
    
    if (!newDivisionId || !name) {
        showStatus('Division ID and Team Name are required', 'error');
        return;
    }
    
    try {
        await adminApi.updateTeam(id, divisionId, {
            divisionId: newDivisionId,
            name,
            captainPlayerId: captainPlayerId || null
        });
        
        showStatus(`Team "${name}" updated successfully`, 'success');
        cancelEditTeam(); // Reset form
        loadTeams(); // Refresh the list
    } catch (error) {
        showStatus(`Error updating team: ${error.message}`, 'error');
    }
}

function cancelEditTeam() {
    const form = document.getElementById('team-form');
    form.reset();
    form.onsubmit = createTeam;
    
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Add Team';
    submitBtn.style.background = '#007bff';
    
    const cancelBtn = form.querySelector('.cancel-edit');
    if (cancelBtn) {
        cancelBtn.remove();
    }
}

function confirmDeleteTeam(id, divisionId, name) {
    if (confirm(`Are you sure you want to delete the team "${name}"? This action cannot be undone.`)) {
        deleteTeam(id, divisionId, name);
    }
}

async function deleteTeam(id, divisionId, name) {
    try {
        await adminApi.deleteTeam(id, divisionId);
        showStatus(`Team "${name}" deleted successfully`, 'success');
        loadTeams(); // Refresh the list
    } catch (error) {
        showStatus(`Error deleting team: ${error.message}`, 'error');
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
        const [memberships, players] = await Promise.all([
            adminApi.getMemberships(teamId),
            adminApi.getPlayers()
        ]);
        
        displayMemberships(memberships, players);
        showStatus(`Loaded ${memberships.length} memberships for team ${teamId}`, 'success');
    } catch (error) {
        showStatus(`Error loading memberships: ${error.message}`, 'error');
    }
}

function displayMemberships(memberships, players) {
    const container = document.getElementById('memberships-list');
    
    if (!memberships || memberships.length === 0) {
        container.innerHTML = '<p>No memberships found</p>';
        return;
    }
    
    // Create a map of player IDs to player names for quick lookup
    const playerMap = {};
    players.forEach(player => {
        playerMap[player.id] = `${player.firstName} ${player.lastName}`;
    });
    
    container.innerHTML = memberships.map(membership => {
        const playerName = playerMap[membership.playerId] || 'Unknown Player';
        const skillLevel = membership.skillLevel_9b ? ` (Skill Level: ${membership.skillLevel_9b})` : '';
        
        return `
            <div class="data-item">
                <div class="data-item-content">
                    <strong>${escapeHtml(playerName)}${skillLevel}</strong>
                    <br><small>Player ID: ${escapeHtml(membership.playerId)}</small>
                    <br><small>Role: ${escapeHtml(membership.role)}</small>
                    <br><small>Membership ID: ${escapeHtml(membership.id)}</small>
                </div>
                <div class="data-item-actions">
                    <button class="btn-edit" onclick="editMembership('${escapeHtml(membership.id)}', '${escapeHtml(membership.teamId)}', '${escapeHtml(membership.divisionId)}', '${escapeHtml(membership.playerId)}', '${escapeHtml(membership.role)}', ${membership.skillLevel_9b || 'null'}, '${escapeHtml(membership.joinedAt)}')">Edit</button>
                    <button class="btn-danger" onclick="confirmDeleteMembership('${escapeHtml(membership.id)}', '${escapeHtml(membership.teamId)}', '${escapeHtml(playerName)}')">Delete</button>
                </div>
            </div>
        `;
    }).join('');
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
            skillLevel_9b: skillLevel ? parseInt(skillLevel) : null
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

function editMembership(id, teamId, divisionId, playerId, role, skillLevel, joinedAt) {
    // Fill the form with current values
    document.getElementById('membership-team-id').value = teamId;
    document.getElementById('membership-division-id').value = divisionId;
    document.getElementById('membership-player-id').value = playerId;
    document.getElementById('membership-role').value = role;
    document.getElementById('membership-skill-level').value = skillLevel || '';
    
    // Change the form to update mode
    const form = document.getElementById('membership-form');
    form.onsubmit = (e) => updateMembership(e, id, teamId, joinedAt);
    
    // Update button text
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Update Membership';
    submitBtn.style.background = '#28a745';
    
    // Add cancel button
    if (!form.querySelector('.cancel-edit')) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.className = 'btn-secondary cancel-edit';
        cancelBtn.style.marginLeft = '0.5rem';
        cancelBtn.onclick = cancelEditMembership;
        submitBtn.parentNode.appendChild(cancelBtn);
    }
}

async function updateMembership(event, id, teamId, joinedAt) {
    event.preventDefault();
    
    const newTeamId = document.getElementById('membership-team-id').value.trim();
    const divisionId = document.getElementById('membership-division-id').value.trim();
    const playerId = document.getElementById('membership-player-id').value.trim();
    const role = document.getElementById('membership-role').value.trim();
    const skillLevel = document.getElementById('membership-skill-level').value.trim();
    
    if (!newTeamId || !divisionId || !playerId || !role) {
        showStatus('Team ID, Division ID, Player ID, and Role are required', 'error');
        return;
    }
    
    try {
        await adminApi.updateMembership(id, teamId, {
            teamId: newTeamId,
            divisionId,
            playerId,
            role,
            skillLevel_9b: skillLevel ? parseInt(skillLevel) : null,
            joinedAt: joinedAt
        });
        
        showStatus(`Membership updated successfully for player ${playerId}`, 'success');
        cancelEditMembership(); // Reset form
        loadMemberships(); // Refresh the list
    } catch (error) {
        showStatus(`Error updating membership: ${error.message}`, 'error');
    }
}

function cancelEditMembership() {
    const form = document.getElementById('membership-form');
    form.reset();
    form.onsubmit = createMembership;
    
    // Reset default values
    document.getElementById('membership-team-id').value = 'team_break_of_dawn_9b';
    document.getElementById('membership-division-id').value = 'div_nottingham_wed_9b_311';
    
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Add Membership';
    submitBtn.style.background = '#007bff';
    
    const cancelBtn = form.querySelector('.cancel-edit');
    if (cancelBtn) {
        cancelBtn.remove();
    }
}

function confirmDeleteMembership(id, teamId, playerName) {
    if (confirm(`Are you sure you want to delete the membership for "${playerName}"?`)) {
        deleteMembership(id, teamId, playerName);
    }
}

async function deleteMembership(id, teamId, playerName) {
    try {
        await adminApi.deleteMembership(id, teamId);
        showStatus(`Membership for "${playerName}" deleted successfully`, 'success');
        loadMemberships(); // Refresh the list
    } catch (error) {
        showStatus(`Error deleting membership: ${error.message}`, 'error');
    }
}

// Match management functions
async function loadMatches() {
    // Hard-code the division ID as requested
    const divisionId = 'div_nottingham_wed_9b_311';
    const startDate = document.getElementById('match-start-date').value;
    const endDate = document.getElementById('match-end-date').value;
    
    try {
        const matches = await adminApi.getMatches(divisionId, startDate || null, endDate || null);
        displayMatches(matches);
        const dateRangeText = (startDate && endDate) ? ` from ${startDate} to ${endDate}` : '';
        showStatus(`Loaded ${matches.length} matches for division ${divisionId}${dateRangeText}`, 'success');
    } catch (error) {
        showStatus(`Error loading matches: ${error.message}`, 'error');
    }
}

function displayMatches(matches) {
    const container = document.getElementById('matches-list');
    
    // Store matches data globally for editing
    currentMatchesData = matches;
    
    if (!matches || matches.length === 0) {
        container.innerHTML = '<p>No matches found</p>';
        return;
    }
    
    container.innerHTML = matches.map(match => {
        const scheduledDate = new Date(match.scheduledAt).toLocaleDateString();
        const scheduledTime = new Date(match.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const homeTeam = match.homeTeamId || 'TBD';
        const awayTeam = match.awayTeamId || 'TBD';
        
        return `
            <div class="match-item">
                <div class="match-header">
                    Week ${match.week}: ${escapeHtml(homeTeam)} vs ${escapeHtml(awayTeam)}
                    <div class="match-actions">
                        <button class="btn-edit" onclick="editMatch('${escapeHtml(match.id)}', '${escapeHtml(match.divisionId)}')">Edit</button>
                        <button class="btn-danger" onclick="confirmDeleteMatch('${escapeHtml(match.id)}', '${escapeHtml(match.divisionId)}', 'Week ${match.week}')">Delete</button>
                    </div>
                </div>
                <div class="match-details">
                    <strong>Date:</strong> ${scheduledDate} ${scheduledTime}<br>
                    <strong>Status:</strong> ${escapeHtml(match.status)}<br>
                    <strong>Match ID:</strong> ${escapeHtml(match.id)}
                </div>
                
                ${match.lineupPlan ? renderLineupPlan(match.lineupPlan, match.id, match.divisionId) : ''}
            </div>
        `;
    }).join('');
}

async function createMatch(event) {
    event.preventDefault();
    
    // Hard-code the division ID as requested
    const divisionId = 'div_nottingham_wed_9b_311';
    const week = document.getElementById('match-week').value.trim();
    const scheduledDate = document.getElementById('match-scheduled-date').value;
    const scheduledTime = document.getElementById('match-scheduled-time').value;
    const homeTeamId = document.getElementById('match-home-team').value.trim();
    const awayTeamId = document.getElementById('match-away-team').value.trim();
    const status = document.getElementById('match-status').value;
    const ruleset = document.getElementById('match-ruleset').value;
    const maxTeamSkillCap = document.getElementById('match-skill-cap').value;
    
    if (!week || !scheduledDate || !scheduledTime) {
        showStatus('Week, date, and time are required', 'error');
        return;
    }
    
    // Combine date and time
    const scheduledAt = new Date(`${scheduledDate}T${scheduledTime}`).toISOString();
    
    try {
        const match = await adminApi.createMatch({
            divisionId,
            week,
            scheduledAt,
            homeTeamId: homeTeamId || null,
            awayTeamId: awayTeamId || null,
            status,
            ruleset,
            maxTeamSkillCap
        });
        
        showStatus(`Match for Week ${week} created successfully`, 'success');
        clearForm('match-form');
        loadMatches(); // Refresh the list
    } catch (error) {
        showStatus(`Error creating match: ${error.message}`, 'error');
    }
}

function editMatch(matchId, divisionId) {
    // First, get the match data from the displayed matches
    const matches = currentMatchesData; // We'll need to store this globally
    const match = matches.find(m => m.id === matchId);
    
    if (!match) {
        showStatus('Match data not found. Please refresh and try again.', 'error');
        return;
    }
    
    // Populate the form with current match data
    document.getElementById('match-week').value = match.week;
    
    // Convert scheduledAt to date and time inputs
    const scheduledDate = new Date(match.scheduledAt);
    document.getElementById('match-scheduled-date').value = scheduledDate.toISOString().split('T')[0];
    document.getElementById('match-scheduled-time').value = scheduledDate.toTimeString().slice(0, 5);
    
    document.getElementById('match-home-team').value = match.homeTeamId || '';
    document.getElementById('match-away-team').value = match.awayTeamId || '';
    document.getElementById('match-status').value = match.status;
    document.getElementById('match-ruleset').value = match.lineupPlan?.ruleset || '9-ball';
    document.getElementById('match-skill-cap').value = match.lineupPlan?.maxTeamSkillCap || 23;
    
    // Change the form to update mode
    const form = document.getElementById('match-form');
    form.onsubmit = (e) => updateMatchForm(e, matchId, divisionId);
    
    // Update button text
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Update Match';
    submitBtn.style.background = '#28a745';
    
    // Add cancel button if it doesn't exist
    if (!form.querySelector('.cancel-edit')) {
        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.className = 'btn-secondary cancel-edit';
        cancelBtn.style.marginLeft = '0.5rem';
        cancelBtn.onclick = cancelEditMatch;
        submitBtn.parentNode.appendChild(cancelBtn);
    }
    
    // Scroll to the form
    form.scrollIntoView({ behavior: 'smooth' });
    showStatus('Match data loaded for editing', 'info');
}

async function updateMatchForm(event, matchId, divisionId) {
    event.preventDefault();
    
    const week = document.getElementById('match-week').value.trim();
    const scheduledDate = document.getElementById('match-scheduled-date').value;
    const scheduledTime = document.getElementById('match-scheduled-time').value;
    const homeTeamId = document.getElementById('match-home-team').value.trim();
    const awayTeamId = document.getElementById('match-away-team').value.trim();
    const status = document.getElementById('match-status').value;
    const ruleset = document.getElementById('match-ruleset').value;
    const maxTeamSkillCap = document.getElementById('match-skill-cap').value;
    
    if (!week || !scheduledDate || !scheduledTime) {
        showStatus('Week, date, and time are required', 'error');
        return;
    }
    
    // Combine date and time
    const scheduledAt = new Date(`${scheduledDate}T${scheduledTime}`).toISOString();
    
    // Get the current match to preserve data we're not editing
    const matches = currentMatchesData;
    const currentMatch = matches.find(m => m.id === matchId);
    
    const updatedMatch = {
        ...currentMatch,
        week: parseInt(week),
        scheduledAt,
        homeTeamId: homeTeamId || null,
        awayTeamId: awayTeamId || null,
        status,
        lineupPlan: {
            ...currentMatch.lineupPlan,
            ruleset,
            maxTeamSkillCap: parseInt(maxTeamSkillCap)
        }
    };
    
    try {
        await adminApi.updateMatch(matchId, divisionId, updatedMatch);
        showStatus(`Match for Week ${week} updated successfully`, 'success');
        cancelEditMatch(); // Reset form
        loadMatches(); // Refresh the list
    } catch (error) {
        showStatus(`Error updating match: ${error.message}`, 'error');
    }
}

function cancelEditMatch() {
    const form = document.getElementById('match-form');
    form.reset();
    form.onsubmit = createMatch;
    
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.textContent = 'Create Match';
    submitBtn.style.background = '#007bff';
    
    const cancelBtn = form.querySelector('.cancel-edit');
    if (cancelBtn) {
        cancelBtn.remove();
    }
}

function confirmDeleteMatch(matchId, divisionId, description) {
    if (confirm(`Are you sure you want to delete match "${description}"?`)) {
        deleteMatch(matchId, divisionId, description);
    }
}

async function deleteMatch(matchId, divisionId, description) {
    try {
        await adminApi.deleteMatch(matchId, divisionId);
        showStatus(`Match "${description}" deleted successfully`, 'success');
        loadMatches(); // Refresh the list
    } catch (error) {
        showStatus(`Error deleting match: ${error.message}`, 'error');
    }
}

function renderLineupPlan(lineup, matchId, divisionId) {
    return `
        <div class="lineup-section">
            <h4>Lineup Plan</h4>
            <div class="match-details">
                <strong>Ruleset:</strong> ${escapeHtml(lineup.ruleset)}<br>
                <strong>Max Skill Cap:</strong> ${lineup.maxTeamSkillCap}<br>
                <strong>Locked:</strong> ${lineup.locked ? 'Yes' : 'No'}
                ${lineup.locked && lineup.lockedBy ? `<br><strong>Locked By:</strong> ${escapeHtml(lineup.lockedBy)}` : ''}
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
            
            <div class="lineup-actions" style="margin-top: 1rem;">
                <button class="btn-secondary" onclick="editLineup('${matchId}', '${divisionId}')">Edit Lineup</button>
            </div>
        </div>
    `;
}

async function editLineup(matchId, divisionId) {
    const matches = currentMatchesData;
    const match = matches.find(m => m.id === matchId);
    
    if (!match) {
        showStatus('Match data not found. Please refresh and try again.', 'error');
        return;
    }
    
    showStatus('Loading team and player data...', 'info');
    
    try {
        // Fetch team memberships and player details
        const teamData = await fetchTeamData(match);
        
        // Store team data globally for use in addPlayer function
        window.currentTeamData = teamData;
        
        // Create lineup editor modal with team data
        const modal = createLineupEditorModal(match, matchId, divisionId, teamData);
        document.body.appendChild(modal);
        
        showStatus('Lineup editor loaded with team data', 'success');
    } catch (error) {
        showStatus(`Error loading team data: ${error.message}`, 'error');
        // Fallback to basic editor without team data
        const modal = createLineupEditorModal(match, matchId, divisionId, {});
        document.body.appendChild(modal);
    }
}

async function fetchTeamData(match) {
    const teamData = {
        homeTeam: { players: [], memberships: [] },
        awayTeam: { players: [], memberships: [] },
        allPlayers: {}
    };
    
    try {
        // Fetch all players once for efficiency
        const allPlayers = await adminApi.getPlayers();
        const playersMap = {};
        allPlayers.forEach(player => {
            playersMap[player.id] = player;
        });

        // Fetch home team data if available
        if (match.homeTeamId) {
            const homeMemberships = await adminApi.getTeamMemberships(match.homeTeamId);
            teamData.homeTeam.memberships = homeMemberships;
            
            // Build player data with skill levels from memberships
            for (const membership of homeMemberships) {
                const player = playersMap[membership.playerId];
                if (player) {
                    const playerWithSkill = {
                        ...player,
                        skillLevel: membership.skillLevel_9b,
                        membership: membership
                    };
                    teamData.homeTeam.players.push(playerWithSkill);
                    teamData.allPlayers[player.id] = playerWithSkill;
                } else {
                    console.warn(`Player not found: ${membership.playerId}`);
                }
            }
        }
        
        // Fetch away team data if available
        if (match.awayTeamId) {
            const awayMemberships = await adminApi.getTeamMemberships(match.awayTeamId);
            teamData.awayTeam.memberships = awayMemberships;
            
            // Build player data with skill levels from memberships
            for (const membership of awayMemberships) {
                const player = playersMap[membership.playerId];
                if (player) {
                    const playerWithSkill = {
                        ...player,
                        skillLevel: membership.skillLevel_9b,
                        membership: membership
                    };
                    teamData.awayTeam.players.push(playerWithSkill);
                    teamData.allPlayers[player.id] = playerWithSkill;
                } else {
                    console.warn(`Player not found: ${membership.playerId}`);
                }
            }
        }
    } catch (error) {
        console.warn('Error fetching team data:', error);
    }
    
    return teamData;
}

function createLineupEditorModal(match, matchId, divisionId, teamData = {}) {
    const modal = document.createElement('div');
    modal.className = 'lineup-modal';
    modal.innerHTML = `
        <div class="lineup-modal-content">
            <div class="lineup-modal-header">
                <h3>Edit Lineup - Week ${match.week}</h3>
                <button class="lineup-close" onclick="closeLineupModal()">&times;</button>
            </div>
            <div class="lineup-modal-body">
                <div class="lineup-info">
                    <div class="form-group">
                        <label>Max Team Skill Cap:</label>
                        <input type="number" id="lineup-skill-cap" value="${match.lineupPlan?.maxTeamSkillCap || 23}" min="1">
                    </div>
                    <div class="form-group">
                        <label>Ruleset:</label>
                        <select id="lineup-ruleset">
                            <option value="9-ball" ${match.lineupPlan?.ruleset === '9-ball' ? 'selected' : ''}>9-Ball</option>
                            <option value="8-ball" ${match.lineupPlan?.ruleset === '8-ball' ? 'selected' : ''}>8-Ball</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>
                            <input type="checkbox" id="lineup-locked" ${match.lineupPlan?.locked ? 'checked' : ''}>
                            Lock Lineup
                        </label>
                    </div>
                </div>
                
                <div class="lineup-teams">
                    <div class="lineup-team-section">
                        <h4>Home Team (${match.homeTeamId || 'TBD'})</h4>
                        ${teamData.homeTeam?.players?.length > 0 ? renderTeamRoster(teamData.homeTeam.players, 'home') : '<p class="no-team-data">No team data available</p>'}
                        <div class="lineup-players-editor" id="home-players">
                            ${renderLineupPlayersEditor(match.lineupPlan?.home || [], 'home', teamData.allPlayers)}
                        </div>
                        <button class="btn-secondary" onclick="addPlayer('home', '${match.homeTeamId || ''}')">Add Player</button>
                        <div class="team-total">
                            Total Skill: <span id="home-total">${match.lineupPlan?.totals?.homePlannedSkillSum || 0}</span>
                        </div>
                    </div>
                    
                    <div class="lineup-team-section">
                        <h4>Away Team (${match.awayTeamId || 'TBD'})</h4>
                        ${teamData.awayTeam?.players?.length > 0 ? renderTeamRoster(teamData.awayTeam.players, 'away') : '<p class="no-team-data">No team data available</p>'}
                        <div class="lineup-players-editor" id="away-players">
                            ${renderLineupPlayersEditor(match.lineupPlan?.away || [], 'away', teamData.allPlayers)}
                        </div>
                        <button class="btn-secondary" onclick="addPlayer('away', '${match.awayTeamId || ''}')">Add Player</button>
                        <div class="team-total">
                            Total Skill: <span id="away-total">${match.lineupPlan?.totals?.awayPlannedSkillSum || 0}</span>
                        </div>
                    </div>
                </div>
            </div>
            <div class="lineup-modal-footer">
                <button class="btn-primary" onclick="saveLineup('${matchId}', '${divisionId}')">Save Lineup</button>
                <button class="btn-secondary" onclick="closeLineupModal()">Cancel</button>
            </div>
        </div>
    `;
    
    // Store team data globally for use in other functions
    window.currentTeamData = teamData;
    
    return modal;
}

function renderTeamRoster(players, team) {
    return `
        <div class="team-roster">
            <h5>Available Players:</h5>
            <div class="roster-players">
                ${players.map(player => `
                    <div class="roster-player" onclick="addPlayerToLineup('${player.id}', '${team}')">
                        <span class="player-name">${escapeHtml(player.firstName)} ${escapeHtml(player.lastName)}</span>
                        <span class="player-skill">(${player.skillLevel || '?'})</span>
                        ${player.apaNumber ? `<span class="player-apa">APA: ${escapeHtml(player.apaNumber)}</span>` : ''}
                    </div>
                `).join('')}
            </div>
        </div>
    `;
}

function addPlayerToLineup(playerId, team) {
    const teamData = window.currentTeamData || {};
    const player = teamData.allPlayers?.[playerId];
    
    if (!player) {
        showStatus('Player data not found', 'error');
        return;
    }
    
    const container = document.getElementById(`${team}-players`);
    const currentPlayers = container.children.length;
    
    const newPlayerHtml = `
        <div class="lineup-player-editor" data-team="${team}" data-index="${currentPlayers}">
            <div class="player-info">
                <input type="text" placeholder="Player ID" value="${playerId}" class="player-id" onchange="updatePlayerInfo(this)" readonly>
                <span class="player-display-name">${escapeHtml(player.firstName)} ${escapeHtml(player.lastName)}</span>
            </div>
            <input type="number" placeholder="Skill" value="${player.skillLevel || ''}" min="1" max="9" class="player-skill" onchange="updateTotals()">
            <input type="number" placeholder="Order" value="${currentPlayers + 1}" min="1" class="player-order">
            <label>
                <input type="checkbox" class="player-alternate">
                Alt
            </label>
            <input type="text" placeholder="Notes" value="" class="player-notes">
            <button class="btn-danger btn-small" onclick="removePlayer('${team}', ${currentPlayers})">×</button>
        </div>
    `;
    container.insertAdjacentHTML('beforeend', newPlayerHtml);
    updateTotals();
    
    showStatus(`Added ${player.firstName} ${player.lastName} to ${team} team`, 'success');
}

function renderLineupPlayersEditor(players, team, allPlayers = {}) {
    return players.map((player, index) => {
        const playerData = allPlayers[player.playerId];
        const displayName = playerData 
            ? `${playerData.firstName} ${playerData.lastName}` 
            : player.playerId;
        const skillLevel = player.skillLevel || playerData?.skillLevel || '';
        
        return `
            <div class="lineup-player-editor" data-team="${team}" data-index="${index}">
                <div class="player-info">
                    <input type="text" placeholder="Player ID" value="${player.playerId}" class="player-id" onchange="updatePlayerInfo(this)">
                    <span class="player-display-name">${escapeHtml(displayName)}</span>
                </div>
                <input type="number" placeholder="Skill" value="${skillLevel}" min="1" max="9" class="player-skill" onchange="updateTotals()">
                <input type="number" placeholder="Order" value="${player.intendedOrder}" min="1" class="player-order">
                <label>
                    <input type="checkbox" ${player.isAlternate ? 'checked' : ''} class="player-alternate">
                    Alt
                </label>
                <input type="text" placeholder="Notes" value="${player.notes || ''}" class="player-notes">
                <button class="btn-danger btn-small" onclick="removePlayer('${team}', ${index})">×</button>
            </div>
        `;
    }).join('');
}

function updatePlayerInfo(input) {
    const playerId = input.value.trim();
    const teamData = window.currentTeamData || {};
    const playerData = teamData.allPlayers?.[playerId];
    const playerEditor = input.closest('.lineup-player-editor');
    const displayNameSpan = playerEditor.querySelector('.player-display-name');
    const skillInput = playerEditor.querySelector('.player-skill');
    
    if (playerData) {
        displayNameSpan.textContent = `${playerData.firstName} ${playerData.lastName}`;
        // Only auto-fill skill if it's currently empty
        if (!skillInput.value) {
            skillInput.value = playerData.skillLevel || '';
        }
        displayNameSpan.style.color = '#28a745'; // Green for found players
    } else {
        displayNameSpan.textContent = playerId || 'Unknown Player';
        displayNameSpan.style.color = '#dc3545'; // Red for unknown players
    }
    
    updateTotals();
}

function addPlayer(team, teamId = '') {
    const container = document.getElementById(`${team}-players`);
    const currentPlayers = container.children.length;
    const newPlayerHtml = `
        <div class="lineup-player-editor" data-team="${team}" data-index="${currentPlayers}">
            <div class="player-info">
                <input type="text" placeholder="Player ID" value="" class="player-id" onchange="updatePlayerInfo(this)">
                <span class="player-display-name">Enter Player ID</span>
            </div>
            <input type="number" placeholder="Skill" value="" min="1" max="9" class="player-skill" onchange="updateTotals()">
            <input type="number" placeholder="Order" value="${currentPlayers + 1}" min="1" class="player-order">
            <label>
                <input type="checkbox" class="player-alternate">
                Alt
            </label>
            <input type="text" placeholder="Notes" value="" class="player-notes">
            <button class="btn-danger btn-small" onclick="removePlayer('${team}', ${currentPlayers})">×</button>
        </div>
    `;
    container.insertAdjacentHTML('beforeend', newPlayerHtml);
    updateTotals();
}

function removePlayer(team, index) {
    const container = document.getElementById(`${team}-players`);
    const playerDiv = container.querySelector(`[data-team="${team}"][data-index="${index}"]`);
    if (playerDiv) {
        playerDiv.remove();
        updateTotals();
    }
}

function updateTotals() {
    const homeTotal = calculateTeamTotal('home');
    const awayTotal = calculateTeamTotal('away');
    
    document.getElementById('home-total').textContent = homeTotal;
    document.getElementById('away-total').textContent = awayTotal;
    
    const skillCap = parseInt(document.getElementById('lineup-skill-cap').value) || 23;
    document.getElementById('home-total').style.color = homeTotal > skillCap ? 'red' : 'green';
    document.getElementById('away-total').style.color = awayTotal > skillCap ? 'red' : 'green';
}

function calculateTeamTotal(team) {
    const container = document.getElementById(`${team}-players`);
    let total = 0;
    
    container.querySelectorAll('.lineup-player-editor').forEach(playerDiv => {
        const skillInput = playerDiv.querySelector('.player-skill');
        const isAlternate = playerDiv.querySelector('.player-alternate').checked;
        
        if (!isAlternate && skillInput.value) {
            total += parseInt(skillInput.value) || 0;
        }
    });
    
    return total;
}

async function saveLineup(matchId, divisionId) {
    const homeTeam = collectTeamData('home');
    const awayTeam = collectTeamData('away');
    const skillCap = parseInt(document.getElementById('lineup-skill-cap').value) || 23;
    const ruleset = document.getElementById('lineup-ruleset').value;
    const locked = document.getElementById('lineup-locked').checked;
    
    const lineupPlan = {
        ruleset,
        maxTeamSkillCap: skillCap,
        home: homeTeam,
        away: awayTeam,
        totals: {
            homePlannedSkillSum: calculateTeamTotal('home'),
            awayPlannedSkillSum: calculateTeamTotal('away'),
            homeWithinCap: calculateTeamTotal('home') <= skillCap,
            awayWithinCap: calculateTeamTotal('away') <= skillCap
        },
        locked,
        lockedBy: locked ? 'Admin' : null,
        lockedAt: locked ? new Date().toISOString() : null,
        history: [] // Would be populated with change history
    };
    
    try {
        await adminApi.updateMatchLineup(matchId, divisionId, lineupPlan);
        showStatus('Lineup updated successfully', 'success');
        closeLineupModal();
        loadMatches(); // Refresh the matches display
    } catch (error) {
        showStatus(`Error updating lineup: ${error.message}`, 'error');
    }
}

function collectTeamData(team) {
    const container = document.getElementById(`${team}-players`);
    const players = [];
    
    container.querySelectorAll('.lineup-player-editor').forEach(playerDiv => {
        const playerId = playerDiv.querySelector('.player-id').value.trim();
        const skillLevel = parseInt(playerDiv.querySelector('.player-skill').value) || 0;
        const intendedOrder = parseInt(playerDiv.querySelector('.player-order').value) || 1;
        const isAlternate = playerDiv.querySelector('.player-alternate').checked;
        const notes = playerDiv.querySelector('.player-notes').value.trim();
        
        if (playerId && skillLevel > 0) {
            players.push({
                playerId,
                skillLevel,
                intendedOrder,
                isAlternate,
                notes: notes || null
            });
        }
    });
    
    return players.sort((a, b) => a.intendedOrder - b.intendedOrder);
}

function closeLineupModal() {
    const modal = document.querySelector('.lineup-modal');
    if (modal) {
        modal.remove();
    }
}

function clearMatchDates() {
    document.getElementById('match-start-date').value = '';
    document.getElementById('match-end-date').value = '';
    loadMatches(); // Reload all matches
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
