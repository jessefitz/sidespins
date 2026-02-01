#!/usr/bin/env python3
"""
import_cosmos_sidespins.py
Upserts Sidespins seed data into Azure Cosmos DB (SQL/Core).

Usage:
  python import_cosmos_sidespins.py --seed ./seed_sidespins.json

Environment variables:
  COSMOS_URI  : Cosmos DB account endpoint (e.g., https://<acct>.documents.azure.com:443/)
  COSMOS_KEY  : Primary key (or resource token)
  COSMOS_DB   : Database name (e.g., sidespins)
Optional flags:
  --create-db           Create database if it doesn't exist
  --throughput 400      Throughput for new containers (ignored for autoscale accounts)
"""
import os
import json
import argparse
from typing import Dict, Any, List
from azure.cosmos import CosmosClient, PartitionKey, exceptions

CONTAINER_SPECS = {
    "Divisions":      {"partition_key": "/id",          "indexing_policy": None},
    "Teams":          {"partition_key": "/divisionId",  "indexing_policy": None},
    "Players":        {"partition_key": "/id",          "indexing_policy": None},
    "TeamMemberships":{"partition_key": "/teamId",      "indexing_policy": None},
    "TeamMatches":    {"partition_key": "/divisionId",  "indexing_policy": None},
    "Sessions":       {"partition_key": "/divisionId",  "indexing_policy": None},
    "Observations":   {"partition_key": "/id",          "indexing_policy": None},
    "Notes":          {"partition_key": "/observationId", "indexing_policy": None},
}

def get_args():
    p = argparse.ArgumentParser()
    p.add_argument("--seed", required=True, help="Path to seed_sidespins.json")
    p.add_argument("--create-db", action="store_true", help="Create database if not exists")
    p.add_argument("--throughput", type=int, default=400, help="Throughput for new containers (RU/s)")
    return p.parse_args()

def get_required_env(name: str) -> str:
    v = os.getenv(name)
    if not v:
        raise SystemExit(f"Missing required env var: {name}")
    return v

def ensure_database(client: CosmosClient, db_name: str, create: bool):
    try:
        db = client.get_database_client(db_name)
        # Probe to confirm access
        _ = list(db.query_containers("SELECT * FROM c OFFSET 0 LIMIT 1"))
        print(f"[ok] Using database: {db_name}")
        return db
    except exceptions.CosmosResourceNotFoundError:
        if not create:
            raise
        print(f"[+] Creating database: {db_name}")
        return client.create_database_if_not_exists(id=db_name)

def ensure_container(db, name: str, spec: Dict[str, Any], throughput: int):
    try:
        container = db.get_container_client(name)
        _ = container.read()
        print(f"[ok] Using container: {name}")
        return container
    except exceptions.CosmosResourceNotFoundError:
        # Try with throughput first (provisioned/autoscale scenarios)
        try:
            print(f"[+] Creating container: {name} (pk={spec['partition_key']}, RU={throughput})")
            return db.create_container(
                id=name,
                partition_key=PartitionKey(path=spec["partition_key"]),
                offer_throughput=throughput
            )
        except exceptions.CosmosHttpResponseError as e:
            msg = str(e)
            # Serverless accounts don't allow offer_throughput/autoscale -- retry without it
            if 'serverless' in msg.lower() or 'not supported for serverless accounts' in msg.lower():
                print(f"[~] Serverless detected. Retrying container create without throughput: {name}")
                return db.create_container(
                    id=name,
                    partition_key=PartitionKey(path=spec["partition_key"]) 
                )
            raise

def upsert_all(container, docs: List[Dict[str, Any]], pk_path: str):
    count = 0
    for d in docs:
        # Minimal guard: ensure partition key field is present
        pk_field = pk_path.lstrip("/")
        if pk_field not in d:
            # Special-case for /id: ensure id exists
            if pk_field == "id" and "id" in d:
                pass
            else:
                raise ValueError(f"Document missing partition key field '{pk_field}': {d.get('id', '<no id>')}")
        container.upsert_item(d)
        count += 1
    print(f"[upserted] {count:>3} docs into {container.container_link.split('/')[-1]}")

def main():
    args = get_args()
    uri = get_required_env("COSMOS_URI")
    key = get_required_env("COSMOS_KEY")
    dbname = get_required_env("COSMOS_DB")

    with open(args.seed, "r", encoding="utf-8") as f:
        seed = json.load(f)

    client = CosmosClient(uri, key)
    db = ensure_database(client, dbname, create=args.create_db)

    # Ensure containers
    containers = {}
    for name, spec in CONTAINER_SPECS.items():
        containers[name] = ensure_container(db, name, spec, args.throughput)

    # Upsert groups in an order that satisfies references
    order = ["Divisions", "Sessions", "Players", "Teams", "TeamMemberships", "TeamMatches"]
    for group in order:
        if group not in seed:
            print(f"[skip] No '{group}' in seed")
            continue
        docs = seed[group]
        upsert_all(containers[group], docs, CONTAINER_SPECS[group]["partition_key"])

    print("\nDone. Tip: check RU charges in Insights logs or enable diagnostics on containers.")

if __name__ == "__main__":
    main()
