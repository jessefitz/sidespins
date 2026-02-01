#!/usr/bin/env python3
"""
import_division.py - Import division teams and players from APA GraphQL API to Cosmos DB

This script fetches team roster data from the external APA GraphQL API and imports
teams and players into the SideSpins Cosmos DB database. It handles player deduplication
by APA member number and supports dry-run mode for preview.

Usage:
    python import_division.py --division-id 418320 \\
        --refresh-token "eyJhbGc..." \\
        --division-name "Nottingham Wednesday 8-Ball" \\
        --cosmos-uri "https://..." \\
        --cosmos-key "..." \\
        --cosmos-db "sidespins" \\
        --what-if
"""

import argparse
import json
import re
import sys
from datetime import datetime
from typing import Dict, List, Optional, Tuple

import requests
from azure.cosmos import CosmosClient, exceptions


# GraphQL API Configuration
GRAPHQL_ENDPOINT = "https://gql.poolplayers.com/graphql"
GRAPHQL_HEADERS = {
    "accept": "*/*",
    "accept-language": "en-US,en;q=0.9",
    "apollographql-client-name": "MemberServices",
    "apollographql-client-version": "3.18.44-3550",
    "content-type": "application/json",
    "origin": "https://league.poolplayers.com",
    "referer": "https://league.poolplayers.com/",
    "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
}


def slugify(text: str) -> str:
    """
    Convert text to a slug format suitable for IDs.
    
    Args:
        text: The text to slugify
        
    Returns:
        Slugified text (lowercase, alphanumeric with underscores)
    """
    # Remove special characters, convert to lowercase
    text = text.lower()
    text = re.sub(r'[^\w\s-]', '', text)
    text = re.sub(r'[\s_-]+', '_', text)
    text = text.strip('_')
    return text


def clean_team_name(team_name: str) -> str:
    """
    Clean team name by removing extraneous data like "(T # 3)".
    
    Args:
        team_name: Raw team name like "We Dem Boyz (T # 3)"
        
    Returns:
        Cleaned team name like "We Dem Boyz"
    """
    # Remove pattern like "(T # 3)" from the end
    cleaned = re.sub(r'\s*\(T\s*#\s*\d+\)\s*$', '', team_name)
    return cleaned.strip()


def split_display_name(display_name: str) -> Tuple[str, str]:
    """
    Split a display name into first and last name.
    
    Args:
        display_name: Full name like "Michael Hayes"
        
    Returns:
        Tuple of (firstName, lastName)
    """
    parts = display_name.strip().split(' ', 1)
    if len(parts) == 2:
        return parts[0], parts[1]
    elif len(parts) == 1:
        return parts[0], ""
    return "", ""


def fetch_access_token(refresh_token: str) -> str:
    """
    Fetch an access token from the GraphQL API using a refresh token.
    
    Args:
        refresh_token: The refresh token for authentication
        
    Returns:
        Access token string
        
    Raises:
        Exception: If the API request fails
    """
    query = """
    mutation GenerateAccessTokenMutation($refreshToken: String!) {
        generateAccessToken(refreshToken: $refreshToken) {
            accessToken
            __typename
        }
    }
    """
    
    payload = [{
        "operationName": "GenerateAccessTokenMutation",
        "variables": {"refreshToken": refresh_token},
        "query": query
    }]
    
    headers = GRAPHQL_HEADERS.copy()
    
    print("Fetching access token...")
    response = requests.post(GRAPHQL_ENDPOINT, headers=headers, json=payload)
    response.raise_for_status()
    
    data = response.json()
    if not data or not data[0].get("data", {}).get("generateAccessToken"):
        raise Exception(f"Failed to get access token: {data}")
    
    access_token = data[0]["data"]["generateAccessToken"]["accessToken"]
    print(f"✓ Access token obtained")
    return access_token


def fetch_division_rosters(access_token: str, division_id: int) -> Dict:
    """
    Fetch division roster data from the GraphQL API.
    
    Args:
        access_token: The access token for authentication
        division_id: The division ID to fetch
        
    Returns:
        Division data dictionary
        
    Raises:
        Exception: If the API request fails
    """
    query = """
    query divisionRosters($id: Int!) {
        division(id: $id) {
            id
            teams {
                isBye
                ...rosterComponent
                location {
                    id
                    name
                    address {
                        id
                        name
                        __typename
                    }
                    __typename
                }
                __typename
            }
            __typename
        }
    }

    fragment rosterComponent on Team {
        id
        name
        number
        league {
            id
            slug
            __typename
        }
        division {
            id
            type
            __typename
        }
        roster {
            id
            memberNumber
            displayName
            matchesWon
            matchesPlayed
            ... on EightBallPlayer {
                pa
                ppm
                skillLevel
                __typename
            }
            ... on NineBallPlayer {
                pa
                ppm
                skillLevel
                __typename
            }
            member {
                id
                __typename
            }
            __typename
        }
        __typename
    }
    """
    
    payload = [{
        "operationName": "divisionRosters",
        "variables": {"id": division_id},
        "query": query
    }]
    
    headers = GRAPHQL_HEADERS.copy()
    headers["authorization"] = access_token
    
    print(f"Fetching division {division_id} rosters...")
    response = requests.post(GRAPHQL_ENDPOINT, headers=headers, json=payload)
    response.raise_for_status()
    
    data = response.json()
    if not data or not data[0].get("data", {}).get("division"):
        raise Exception(f"Failed to get division rosters: {data}")
    
    division_data = data[0]["data"]["division"]
    team_count = len([t for t in division_data["teams"] if not t.get("isBye")])
    print(f"✓ Division data received: {team_count} teams")
    return division_data


def check_player_exists(players_container, apa_number: str) -> Optional[Dict]:
    """
    Check if a player already exists in the database by APA number.
    
    Args:
        players_container: Cosmos DB players container client
        apa_number: APA member number to check
        
    Returns:
        Existing player document or None
    """
    player_id = f"p_{apa_number}"
    try:
        existing_player = players_container.read_item(
            item=player_id,
            partition_key=player_id
        )
        return existing_player
    except exceptions.CosmosResourceNotFoundError:
        return None


def check_team_exists(teams_container, apa_team_id: str, division_id: str) -> Optional[Dict]:
    """
    Check if a team already exists in the database by APA team ID.
    
    Args:
        teams_container: Cosmos DB teams container client
        apa_team_id: APA team ID to check
        division_id: Division partition key
        
    Returns:
        Existing team document or None
    """
    query = "SELECT * FROM c WHERE c.apaTeamId = @apaTeamId AND c.divisionId = @divisionId"
    parameters = [
        {"name": "@apaTeamId", "value": apa_team_id},
        {"name": "@divisionId", "value": division_id}
    ]
    
    items = list(teams_container.query_items(
        query=query,
        parameters=parameters,
        enable_cross_partition_query=True
    ))
    
    return items[0] if items else None


def compare_names(display_name: str, first_name: str, last_name: str) -> bool:
    """
    Compare display name with stored first/last name to detect mismatches.
    
    Args:
        display_name: Full name from API
        first_name: Stored first name
        last_name: Stored last name
        
    Returns:
        True if names match (case-insensitive), False otherwise
    """
    stored_full = f"{first_name} {last_name}".strip().lower()
    api_full = display_name.strip().lower()
    return stored_full == api_full


def transform_division(division_data: Dict, division_name: str, timestamp: str) -> Dict:
    """
    Transform GraphQL division data to SideSpins Division model.
    
    Args:
        division_data: Raw GraphQL division data
        division_name: Human-readable division name
        timestamp: ISO timestamp for createdAt
        
    Returns:
        Division document
    """
    division_id = f"div_{division_data['id']}"
    
    # Infer division type from first non-bye team
    division_type = None
    for team in division_data["teams"]:
        if not team.get("isBye") and team.get("division", {}).get("type"):
            division_type = team["division"]["type"]
            break
    
    # Map EIGHT/NINE to "8-ball"/"9-ball"
    type_map = {"EIGHT": "8-ball", "NINE": "9-ball"}
    game_type = type_map.get(division_type, "8-ball")
    
    return {
        "id": division_id,
        "type": "division",
        "league": "APA",
        "name": division_name,
        "area": "",  # Not available from API
        "createdAt": timestamp
    }


def transform_team(team_data: Dict, division_id: str, captain_player_id: str, timestamp: str) -> Dict:
    """
    Transform GraphQL team data to SideSpins Team model.
    
    Args:
        team_data: Raw GraphQL team data
        division_id: Parent division ID
        captain_player_id: Player ID of team captain
        timestamp: ISO timestamp for createdAt
        
    Returns:
        Team document
    """
    # Clean the team name (remove "(T # N)" suffix)
    clean_name = clean_team_name(team_data["name"])
    team_name_slug = slugify(clean_name)
    team_number = team_data.get("number", "")
    team_id = f"team_{team_name_slug}_{team_number}"
    
    return {
        "id": team_id,
        "type": "team",
        "divisionId": division_id,
        "name": clean_name,
        "apaTeamId": str(team_data["id"]),
        "captainPlayerId": captain_player_id,
        "createdAt": timestamp
    }


def transform_player(roster_entry: Dict, timestamp: str) -> Dict:
    """
    Transform GraphQL roster entry to SideSpins Player model.
    
    Args:
        roster_entry: Raw GraphQL roster data
        timestamp: ISO timestamp for createdAt
        
    Returns:
        Player document
    """
    apa_number = roster_entry["memberNumber"]
    player_id = f"p_{apa_number}"
    first_name, last_name = split_display_name(roster_entry["displayName"])
    
    return {
        "id": player_id,
        "type": "player",
        "firstName": first_name,
        "lastName": last_name,
        "apaNumber": apa_number,
        "createdAt": timestamp
    }


def transform_membership(
    roster_entry: Dict,
    team_id: str,
    division_id: str,
    player_id: str,
    division_type: str,
    timestamp: str
) -> Dict:
    """
    Transform GraphQL roster entry to SideSpins TeamMembership model.
    
    Args:
        roster_entry: Raw GraphQL roster data
        team_id: Parent team ID
        division_id: Parent division ID
        player_id: Player ID
        division_type: "EIGHT" or "NINE" from API
        timestamp: ISO timestamp for joinedAt
        
    Returns:
        TeamMembership document
    """
    membership_id = f"m_{team_id}_{player_id}"
    
    # Determine which skill level field to populate
    skill_level_field = "skillLevel_8b" if division_type == "EIGHT" else "skillLevel_9b"
    skill_level = roster_entry.get("skillLevel", 0)
    
    membership = {
        "id": membership_id,
        "type": "membership",
        "teamId": team_id,
        "divisionId": division_id,
        "playerId": player_id,
        "role": "player",
        "joinedAt": timestamp,
        "leftAt": None
    }
    
    # Set the appropriate skill level field
    membership[skill_level_field] = skill_level
    
    return membership


def import_division(
    division_id: int,
    refresh_token: str,
    division_name: str,
    cosmos_uri: str,
    cosmos_key: str,
    cosmos_db: str,
    what_if: bool = False,
    sidespins_division_id: str = None
):
    """
    Main import function to fetch and import division data.
    
    Args:
        division_id: Division ID to import
        refresh_token: API refresh token
        division_name: Human-readable division name
        cosmos_uri: Cosmos DB endpoint URI
        cosmos_key: Cosmos DB access key
        cosmos_db: Cosmos DB database name
        what_if: If True, preview changes without committing
        sidespins_division_id: Existing SideSpins division ID to import into (optional)
    """
    timestamp = datetime.utcnow().isoformat() + 'Z'
    
    # Statistics tracking
    stats = {
        "divisions_created": 0,
        "teams_created": 0,
        "players_created": 0,
        "players_skipped": 0,
        "memberships_created": 0,
        "warnings": []
    }
    
    # Fetch data from API
    access_token = fetch_access_token(refresh_token)
    division_data = fetch_division_rosters(access_token, division_id)
    
    # Connect to Cosmos DB (always connect for existence checks, even in what-if mode)
    print(f"\nConnecting to Cosmos DB: {cosmos_db}...")
    client = CosmosClient(cosmos_uri, cosmos_key)
    database = client.get_database_client(cosmos_db)
    divisions_container = database.get_container_client("Divisions")
    teams_container = database.get_container_client("Teams")
    players_container = database.get_container_client("Players")
    memberships_container = database.get_container_client("TeamMemberships")
    print("✓ Connected to Cosmos DB")
    
    if what_if:
        print("\n[WHAT-IF MODE] - No changes will be made to the database")
    
    # Transform and import division
    print(f"\n{'='*60}")
    print("DIVISION")
    print(f"{'='*60}")
    
    if sidespins_division_id:
        # Use existing division
        print(f"Using existing SideSpins division: {sidespins_division_id}")
        division_doc = {"id": sidespins_division_id}
        stats["divisions_created"] = 0
    else:
        # Create new division from APA data
        division_doc = transform_division(division_data, division_name, timestamp)
        
        if what_if:
            print(f"[WHAT-IF] Would create/update division:")
            print(json.dumps(division_doc, indent=2))
            stats["divisions_created"] = 1
        else:
            divisions_container.upsert_item(division_doc)
            print(f"✓ Division upserted: {division_doc['id']}")
            stats["divisions_created"] = 1
    
    # Process teams
    print(f"\n{'='*60}")
    print("TEAMS & PLAYERS")
    print(f"{'='*60}")
    
    for team_data in division_data["teams"]:
        # Skip bye teams
        if team_data.get("isBye"):
            print(f"\nSkipping bye team: {team_data.get('name', 'Unknown')}")
            continue
        
        # Skip teams that already exist in the database
        apa_team_id = str(team_data["id"])
        existing_team = check_team_exists(teams_container, apa_team_id, division_doc["id"])
        
        if existing_team:
            print(f"\nSkipping existing team (APA ID {apa_team_id}): {team_data.get('name', 'Unknown')}")
            stats["teams_skipped"] = stats.get("teams_skipped", 0) + 1
            continue
        
        roster = team_data.get("roster", [])
        if not roster:
            print(f"\nSkipping team with no roster: {team_data.get('name', 'Unknown')}")
            continue
        
        # Get division type for this team
        division_type = team_data.get("division", {}).get("type", "EIGHT")
        
        # Clean team name for display
        clean_name = clean_team_name(team_data["name"])
        print(f"\n--- Team: {clean_name} (#{team_data.get('number', 'N/A')}) ---")
        
        # First roster player is captain
        captain_roster_entry = roster[0]
        captain_apa_number = captain_roster_entry["memberNumber"]
        captain_player_id = f"p_{captain_apa_number}"
        
        # Transform team
        team_doc = transform_team(
            team_data,
            division_doc["id"],
            captain_player_id,
            timestamp
        )
        
        if what_if:
            print(f"[WHAT-IF] Would create/update team:")
            print(json.dumps(team_doc, indent=2))
            stats["teams_created"] += 1
        else:
            teams_container.upsert_item(team_doc)
            print(f"✓ Team upserted: {team_doc['id']}")
            stats["teams_created"] += 1
        
        # Process players and memberships
        print(f"  Players ({len(roster)}):")
        for idx, roster_entry in enumerate(roster):
            apa_number = roster_entry["memberNumber"]
            player_id = f"p_{apa_number}"
            display_name = roster_entry["displayName"]
            skill_level = roster_entry.get("skillLevel", "?")
            is_captain = (idx == 0)
            
            # Check if player exists
            existing_player = check_player_exists(players_container, apa_number)
            
            if existing_player:
                # Player exists - check for name mismatch
                if not compare_names(
                    display_name,
                    existing_player.get("firstName", ""),
                    existing_player.get("lastName", "")
                ):
                    warning = (
                        f"Name mismatch for APA#{apa_number}: "
                        f"API='{display_name}' vs "
                        f"DB='{existing_player.get('firstName', '')} {existing_player.get('lastName', '')}'"
                    )
                    print(f"  ⚠  {display_name} (APA#{apa_number}) - {warning}")
                    stats["warnings"].append(warning)
                else:
                    print(f"  ○ {display_name} (APA#{apa_number}, SL{skill_level}) - Exists{' [CAPTAIN]' if is_captain else ''}")
                stats["players_skipped"] += 1
            else:
                # Create new player
                player_doc = transform_player(roster_entry, timestamp)
                
                if what_if:
                    print(f"  [WHAT-IF] Would create player: {display_name} (APA#{apa_number}, SL{skill_level}){' [CAPTAIN]' if is_captain else ''}")
                    stats["players_created"] += 1
                else:
                    players_container.upsert_item(player_doc)
                    print(f"  ✓ {display_name} (APA#{apa_number}, SL{skill_level}) - Created{' [CAPTAIN]' if is_captain else ''}")
                    stats["players_created"] += 1
            
            # Create membership
            membership_doc = transform_membership(
                roster_entry,
                team_doc["id"],
                division_doc["id"],
                player_id,
                division_type,
                timestamp
            )
            
            if what_if:
                # Count memberships in what-if mode too
                stats["memberships_created"] += 1
            else:
                memberships_container.upsert_item(membership_doc)
                stats["memberships_created"] += 1
    
    # Print summary
    print(f"\n{'='*60}")
    print("IMPORT SUMMARY")
    print(f"{'='*60}")
    print(f"Divisions:   {stats['divisions_created']} created/updated")
    print(f"Teams:       {stats['teams_created']} created/updated")
    print(f"Players:     {stats['players_created']} created, {stats['players_skipped']} skipped (existing)")
    print(f"Memberships: {stats['memberships_created']} created/updated")
    
    if stats["warnings"]:
        print(f"\n⚠ WARNINGS ({len(stats['warnings'])}):")
        for warning in stats["warnings"]:
            print(f"  - {warning}")
    
    if what_if:
        print("\n[WHAT-IF MODE] - No actual changes were made")
    else:
        print("\n✓ Import completed successfully")


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description="Import division teams and players from APA GraphQL API to Cosmos DB"
    )
    parser.add_argument(
        "--division-id",
        type=int,
        required=True,
        help="Division ID to import (e.g., 418320)"
    )
    parser.add_argument(
        "--refresh-token",
        required=True,
        help="API refresh token for authentication"
    )
    parser.add_argument(
        "--division-name",
        required=True,
        help="Human-readable division name (e.g., 'Nottingham Wednesday 8-Ball')"
    )
    parser.add_argument(
        "--cosmos-uri",
        required=True,
        help="Cosmos DB endpoint URI"
    )
    parser.add_argument(
        "--cosmos-key",
        required=True,
        help="Cosmos DB access key"
    )
    parser.add_argument(
        "--cosmos-db",
        required=True,
        help="Cosmos DB database name"
    )
    parser.add_argument(
        "--what-if",
        action="store_true",
        help="Preview changes without committing to database"
    )
    parser.add_argument(
        "--sidespins-division-id",
        help="Existing SideSpins division ID to import teams into (skips division creation)"
    )
    
    args = parser.parse_args()
    
    try:
        import_division(
            division_id=args.division_id,
            refresh_token=args.refresh_token,
            division_name=args.division_name,
            cosmos_uri=args.cosmos_uri,
            cosmos_key=args.cosmos_key,
            cosmos_db=args.cosmos_db,
            what_if=args.what_if,
            sidespins_division_id=args.sidespins_division_id
        )
    except Exception as e:
        print(f"\n❌ Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
