#!/usr/bin/env python3
"""
import_schedule.py - Import division schedule/matches from APA GraphQL API to Cosmos DB

This script fetches match schedule data from the external APA GraphQL API and imports
matches into the SideSpins Cosmos DB TeamMatches container. It only creates new matches
and preserves existing matches to avoid overwriting user-entered lineup and score data.

Usage:
    python import_schedule.py --division-id 418320 \\
        --refresh-token "eyJhbGc..." \\
        --session-id "session_2025_fall" \\
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
from typing import Dict, List, Optional

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


def fetch_division_schedule(access_token: str, division_id: int) -> Dict:
    """
    Fetch division schedule data from the GraphQL API.
    
    Args:
        access_token: The access token for authentication
        division_id: The division ID to fetch
        
    Returns:
        Division data dictionary with schedule
        
    Raises:
        Exception: If the API request fails
    """
    query = """
    query divisionSchedule($id: Int!) {
        division(id: $id) {
            id
            teams {
                id
                name
                number
                isBye
                __typename
            }
            schedule {
                id
                description
                date
                weekOfPlay
                skip
                matches {
                    id
                    isBye
                    status
                    startTime
                    results {
                        homeAway
                        points {
                            total
                            __typename
                        }
                        __typename
                    }
                    home {
                        id
                        name
                        number
                        __typename
                    }
                    away {
                        id
                        name
                        number
                        __typename
                    }
                    __typename
                }
                __typename
            }
            __typename
        }
    }
    """
    
    payload = [{
        "operationName": "divisionSchedule",
        "variables": {"id": division_id},
        "query": query
    }]
    
    headers = GRAPHQL_HEADERS.copy()
    headers["authorization"] = access_token
    
    print(f"Fetching division {division_id} schedule...")
    response = requests.post(GRAPHQL_ENDPOINT, headers=headers, json=payload)
    response.raise_for_status()
    
    data = response.json()
    if not data or not data[0].get("data", {}).get("division"):
        raise Exception(f"Failed to get division schedule: {data}")
    
    division_data = data[0]["data"]["division"]
    total_matches = sum(len(s["matches"]) for s in division_data["schedule"] if not s.get("skip"))
    print(f"✓ Schedule data received: {len(division_data['schedule'])} weeks, {total_matches} matches")
    return division_data


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


def build_team_mapping_from_db(teams_container, division_id: str) -> Dict[str, Dict]:
    """
    Build a mapping from API team numbers to database team IDs by querying Cosmos DB.
    
    Args:
        teams_container: Cosmos DB teams container client
        division_id: Division ID to query
        
    Returns:
        Dict mapping team numbers (e.g., "03301") to {id, name}
    """
    query = "SELECT * FROM c WHERE c.divisionId = @divisionId"
    parameters = [{"name": "@divisionId", "value": division_id}]
    
    teams = list(teams_container.query_items(
        query=query,
        parameters=parameters,
        enable_cross_partition_query=False
    ))
    
    team_map = {}
    for team in teams:
        # Extract team number from team ID (last component after final underscore)
        # E.g., "team_we_dem_boyz_03306" -> "03306"
        team_id = team["id"]
        parts = team_id.split("_")
        if len(parts) > 0:
            team_number = parts[-1]
            team_map[team_number] = {
                "id": team_id,
                "name": team["name"]
            }
    
    return team_map


def transform_match(
    match_data: Dict,
    week: int,
    division_id: str,
    session_id: str,
    team_map: Dict[str, Dict],
    timestamp: str
) -> Optional[Dict]:
    """
    Transform GraphQL match data to SideSpins TeamMatch model.
    
    Args:
        match_data: Raw GraphQL match data
        week: Week of play number
        division_id: Division ID
        session_id: Session ID
        team_map: Mapping of team numbers to team info
        timestamp: ISO timestamp for createdAt
        
    Returns:
        TeamMatch document or None if bye match or team not found
    """
    # Skip bye matches
    if match_data.get("isBye"):
        return None
    
    # Get home and away team info
    home_number = match_data["home"]["number"]
    away_number = match_data["away"]["number"]
    
    home_info = team_map.get(home_number)
    away_info = team_map.get(away_number)
    
    # Skip if teams not found (with warning logged by caller)
    if not home_info or not away_info:
        return None
    
    home_team_id = home_info["id"]
    away_team_id = away_info["id"]
    home_team_name = home_info["name"]
    away_team_name = away_info["name"]
    
    # Generate match ID using session and team IDs
    match_id = f"match_{session_id}_{week}_{home_team_id}_{away_team_id}"
    
    # Parse scheduled time
    scheduled_at = match_data.get("startTime", "")
    if scheduled_at:
        # Convert to ISO format
        try:
            # Handle timezone offset format like "2025-12-08T19:00:00-05:00"
            dt = datetime.fromisoformat(scheduled_at.replace('Z', '+00:00'))
            scheduled_at = dt.isoformat()
            if not scheduled_at.endswith('Z') and '+' not in scheduled_at and '-' not in scheduled_at[-6:]:
                scheduled_at += 'Z'
        except:
            scheduled_at = timestamp
    else:
        scheduled_at = timestamp
    
    # Map status
    status_map = {
        "COMPLETED": "completed",
        "UNPLAYED": "scheduled",
        "SCHEDULED": "scheduled"
    }
    status = status_map.get(match_data.get("status", "UNPLAYED"), "scheduled")
    
    # Extract results if completed
    totals = {
        "homePoints": 0,
        "awayPoints": 0,
        "bonusPoints": {
            "home": 0,
            "away": 0
        }
    }
    
    if status == "completed" and match_data.get("results"):
        for result in match_data["results"]:
            if result["homeAway"] == "HOME":
                totals["homePoints"] = result["points"]["total"]
            elif result["homeAway"] == "AWAY":
                totals["awayPoints"] = result["points"]["total"]
    
    return {
        "id": match_id,
        "type": "teamMatch",
        "divisionId": division_id,
        "sessionId": session_id,
        "week": week,
        "scheduledAt": scheduled_at,
        "homeTeamId": home_team_id,
        "homeTeamName": home_team_name,
        "awayTeamId": away_team_id,
        "awayTeamName": away_team_name,
        "status": status,
        "lineupPlan": {
            "ruleset": "",
            "maxTeamSkillCap": 23,
            "home": [],
            "away": [],
            "totals": {
                "homePlannedSkillSum": 0,
                "awayPlannedSkillSum": 0,
                "homeWithinCap": True,
                "awayWithinCap": True
            },
            "locked": False,
            "lockedBy": None,
            "lockedAt": None,
            "history": []
        },
        "playerMatches": [],
        "totals": totals,
        "createdAt": timestamp
    }


def import_schedule(
    division_id: int,
    refresh_token: str,
    session_id: str,
    cosmos_uri: str,
    cosmos_key: str,
    cosmos_db: str,
    what_if: bool = False
):
    """
    Main import function to fetch and import schedule data.
    
    Args:
        division_id: Division ID to import
        refresh_token: API refresh token
        session_id: Session ID to link matches to
        cosmos_uri: Cosmos DB endpoint URI
        cosmos_key: Cosmos DB access key
        cosmos_db: Cosmos DB database name
        what_if: If True, preview changes without committing
    """
    timestamp = datetime.utcnow().isoformat() + 'Z'
    
    # Statistics tracking
    stats = {
        "weeks_processed": 0,
        "matches_created": 0,
        "matches_skipped_exists": 0,
        "matches_skipped_bye": 0,
        "matches_skipped_no_team": 0,
        "warnings": []
    }
    
    # Fetch data from API
    access_token = fetch_access_token(refresh_token)
    division_data = fetch_division_schedule(access_token, division_id)
    
    # Build our division ID
    our_division_id = f"div_{division_id}"
    
    # Connect to Cosmos DB
    if not what_if:
        print(f"\nConnecting to Cosmos DB: {cosmos_db}...")
        client = CosmosClient(cosmos_uri, cosmos_key)
        database = client.get_database_client(cosmos_db)
        teams_container = database.get_container_client("Teams")
        matches_container = database.get_container_client("TeamMatches")
        print("✓ Connected to Cosmos DB")
    else:
        print("\n[WHAT-IF MODE] - No changes will be made to the database")
        teams_container = None
        matches_container = None
    
    # Build team mapping from database
    print(f"\nBuilding team mapping from database...")
    if not what_if:
        team_map = build_team_mapping_from_db(teams_container, our_division_id)
        print(f"✓ Found {len(team_map)} teams in database")
    else:
        # In what-if mode, build mapping from API data
        team_map = {}
        for team in division_data["teams"]:
            if not team.get("isBye"):
                team_number = team["number"]
                clean_name = clean_team_name(team["name"])
                team_id = f"team_{slugify(clean_name)}_{team_number}"
                team_map[team_number] = {
                    "id": team_id,
                    "name": clean_name
                }
        print(f"✓ [WHAT-IF] Simulated {len(team_map)} team mappings")
    
    # Process schedule
    print(f"\n{'='*60}")
    print("SCHEDULE & MATCHES")
    print(f"{'='*60}")
    
    for schedule_entry in division_data["schedule"]:
        # Skip entries marked as skip or with null weekOfPlay
        if schedule_entry.get("skip") or schedule_entry.get("weekOfPlay") is None:
            description = schedule_entry.get("description", "N/A")
            print(f"\nSkipping week: {description}")
            continue
        
        week = schedule_entry["weekOfPlay"]
        date = schedule_entry.get("date", "")
        description = schedule_entry.get("description", "")
        matches = schedule_entry.get("matches", [])
        
        print(f"\n--- Week {week}: {description} ({date[:10] if date else 'N/A'}) ---")
        
        if not matches:
            print("  No matches scheduled")
            stats["weeks_processed"] += 1
            continue
        
        # Process matches for this week
        for match_data in matches:
            # Skip bye matches
            if match_data.get("isBye"):
                stats["matches_skipped_bye"] += 1
                continue
            
            # Check if teams exist
            home_number = match_data["home"]["number"]
            away_number = match_data["away"]["number"]
            
            if home_number not in team_map or away_number not in team_map:
                home_name = match_data["home"]["name"]
                away_name = match_data["away"]["name"]
                warning = f"Week {week}: Teams not found in DB - {home_name} (#{home_number}) vs {away_name} (#{away_number})"
                print(f"  ⚠ {warning}")
                stats["warnings"].append(warning)
                stats["matches_skipped_no_team"] += 1
                continue
            
            # Transform match
            match_doc = transform_match(
                match_data,
                week,
                our_division_id,
                session_id,
                team_map,
                timestamp
            )
            
            if not match_doc:
                stats["matches_skipped_bye"] += 1
                continue
            
            home_name = match_doc["homeTeamName"]
            away_name = match_doc["awayTeamName"]
            status = match_doc["status"]
            status_emoji = "✓" if status == "completed" else "○"
            
            if what_if:
                # Check if match would exist (simulate by ID)
                print(f"  [WHAT-IF] {status_emoji} {home_name} vs {away_name} - Would create")
                if status == "completed":
                    print(f"    Score: {match_doc['totals']['homePoints']} - {match_doc['totals']['awayPoints']}")
                stats["matches_created"] += 1
            else:
                # Check if match already exists
                try:
                    existing = matches_container.read_item(
                        item=match_doc["id"],
                        partition_key=our_division_id
                    )
                    # Match exists - skip to preserve user data
                    print(f"  ○ {home_name} vs {away_name} - Already exists (skipped)")
                    stats["matches_skipped_exists"] += 1
                except exceptions.CosmosResourceNotFoundError:
                    # Create new match
                    matches_container.upsert_item(match_doc)
                    print(f"  {status_emoji} {home_name} vs {away_name} - Created")
                    if status == "completed":
                        print(f"    Score: {match_doc['totals']['homePoints']} - {match_doc['totals']['awayPoints']}")
                    stats["matches_created"] += 1
        
        stats["weeks_processed"] += 1
    
    # Print summary
    print(f"\n{'='*60}")
    print("IMPORT SUMMARY")
    print(f"{'='*60}")
    print(f"Weeks:   {stats['weeks_processed']} processed")
    print(f"Matches: {stats['matches_created']} created")
    print(f"         {stats['matches_skipped_exists']} skipped (already exist)")
    print(f"         {stats['matches_skipped_bye']} skipped (bye)")
    print(f"         {stats['matches_skipped_no_team']} skipped (team not found)")
    
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
        description="Import division schedule/matches from APA GraphQL API to Cosmos DB"
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
        "--session-id",
        required=True,
        help="Session ID to link matches to (e.g., 'session_2025_fall')"
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
    
    args = parser.parse_args()
    
    try:
        import_schedule(
            division_id=args.division_id,
            refresh_token=args.refresh_token,
            session_id=args.session_id,
            cosmos_uri=args.cosmos_uri,
            cosmos_key=args.cosmos_key,
            cosmos_db=args.cosmos_db,
            what_if=args.what_if
        )
    except Exception as e:
        print(f"\n❌ Error: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
