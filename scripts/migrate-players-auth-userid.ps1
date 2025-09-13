# PowerShell script to add authUserId to existing player documents
# This script updates player documents in Cosmos DB to include authUserId

param(
    [Parameter(Mandatory=$true)]
    [string]$CosmosEndpoint,
    
    [Parameter(Mandatory=$true)]
    [string]$CosmosKey,
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "sidespins",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "Players"
)

# Install required module if not present
if (!(Get-Module -ListAvailable -Name CosmosDB)) {
    Install-Module -Name CosmosDB -Force -AllowClobber
}

Import-Module CosmosDB

# Connect to Cosmos DB
$cosmosDbContext = New-CosmosDbContext -Account $CosmosEndpoint.Replace("https://", "").Replace(".documents.azure.com:443/", "") -Key $CosmosKey

try {
    Write-Host "Fetching all player documents..." -ForegroundColor Green
    
    # Get all player documents
    $players = Get-CosmosDbDocument -Context $cosmosDbContext -Database $DatabaseName -CollectionId $ContainerName -Query "SELECT * FROM c WHERE c.type = 'player'"
    
    Write-Host "Found $($players.Count) player documents" -ForegroundColor Yellow
    
    foreach ($player in $players) {
        if (-not $player.authUserId) {
            Write-Host "Processing player: $($player.id) - $($player.firstName) $($player.lastName)" -ForegroundColor Cyan
            
            # Prompt for authUserId for each player
            $authUserId = Read-Host "Enter authUserId for $($player.firstName) $($player.lastName) (ID: $($player.id)) [or 'skip' to skip]"
            
            if ($authUserId -and $authUserId -ne "skip") {
                # Add authUserId to the document
                $player.authUserId = $authUserId
                
                # Update the document
                Set-CosmosDbDocument -Context $cosmosDbContext -Database $DatabaseName -CollectionId $ContainerName -Id $player.id -DocumentBody $player -PartitionKey $player.id
                
                Write-Host "✓ Updated player $($player.id) with authUserId: $authUserId" -ForegroundColor Green
            }
            else {
                Write-Host "⏭ Skipped player $($player.id)" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "✓ Player $($player.id) already has authUserId: $($player.authUserId)" -ForegroundColor Green
        }
    }
    
    Write-Host "Migration completed!" -ForegroundColor Green
}
catch {
    Write-Error "Error during migration: $($_.Exception.Message)"
}
