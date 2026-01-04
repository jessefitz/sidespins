#!/usr/bin/env python3
"""Check and fix Sessions container partition key."""
import os
from azure.cosmos import CosmosClient, PartitionKey, exceptions

def main():
    uri = os.getenv("COSMOS_URI")
    key = os.getenv("COSMOS_KEY")
    dbname = os.getenv("COSMOS_DB")
    
    if not all([uri, key, dbname]):
        print("Missing COSMOS_URI, COSMOS_KEY, or COSMOS_DB environment variables")
        return
    
    client = CosmosClient(uri, key)
    db = client.get_database_client(dbname)
    
    try:
        # Try to get the container
        container = db.get_container_client("Sessions")
        props = container.read()
        
        print(f"Sessions container found!")
        print(f"Partition key path: {props['partitionKey']['paths']}")
        print(f"Partition key kind: {props['partitionKey'].get('kind', 'Hash')}")
        
        # Check if it's the correct partition key
        if props['partitionKey']['paths'] == ['/divisionId']:
            print("✓ Partition key is correct: /divisionId")
        else:
            print(f"✗ WRONG partition key! Expected ['/divisionId'], got {props['partitionKey']['paths']}")
            print("\nTo fix this, you need to:")
            print("1. Delete the Sessions container")
            print("2. Recreate it with partition key /divisionId")
            print("\nRun: python import_cosmos_sidespins.py --seed ./seed_sidespins.json")
            
    except exceptions.CosmosResourceNotFoundError:
        print("Sessions container not found!")
        print("Run: python import_cosmos_sidespins.py --seed ./seed_sidespins.json --create-db")

if __name__ == "__main__":
    main()
