# Import the Cosmos DB PowerShell module
Import-Module Az.CosmosDB


# Authenticate with Azure and persist the authentication context for the current session
Connect-AzAccount -Subscription "c5456ec6-c29f-47b2-82a5-70ff5f9acc35" -Scope Process

# Get the path to local.settings.json relative to the script location
$localSettingsPath = "C:\projects\jessfitz.me\jessefitz_app\api\local.settings.json"

# Read the Cosmos DB account information from local.settings.json
$localSettings = Get-Content $localSettingsPath | ConvertFrom-Json
$cosmosDbAccountName = $localSettings.Values.CosmosAccountName
$resourceGroupName = $localSettings.Values.ResourceGroupName
$databaseName = $localSettings.Values.CosmosDBName
$containerName = $localSettings.Values.ComsosContainerName
$keyString = $localSettings.Values.CosmosKey 
$endPoint = $localSettings.Values.CosmosEndPoint

# Set the file path and read its contents
$filePath = "C:\projects\jessfitz.me\jessefitz_app\src\assets\article-content\bridge.md"
$content = Get-Content $filePath -Raw

# Create a new document to insert into Cosmos DB
$document = @{
    "id" = [guid]::NewGuid().ToString()
    "content" = $content
}

$primaryKey = ConvertTo-SecureString -String $keyString -AsPlainText -Force
#$cosmosDbContext = New-CosmosDbContext -Account $cosmosDbAccountName -Database $databaseName -Key $primaryKey -EndpointHostname $endPoint
$cosmosDbContext = New-CosmosDbContext -Account $cosmosDbAccountName -Database $databaseName -ResourceGroup $resourceGroupName
# Insert the document into Cosmos DB
New-CosmosDbDocument -Context $cosmosDbContext -CollectionId $containerName -DocumentBody $document