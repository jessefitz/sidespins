# Deployment and Configuration

## DB

Data for SideSpins is hosted in Azure Cosmos DB.  The database and containers are created and seeded with data using the `importcosmos_sidespins.py` script.  Review the script's comments for detailed instructions.

## API

SideSpins API is exposed through a series of Azure Functions in the SideSpinsApi Function App.

Notes:

- Function App is manually deployed to Azure via the VS Code Azure extension.
- App configurations are manually applied via the Function App's environment variable settings.
- CORS configurations are manually applied via the Function App's CORS settings.

## Site

SideSpins uses a Jekyll front-end hosted in GitHub pages.  The site is compiled and deployed using GitHub's built-in "build and deploy pages" action, which is configured from the GitHub Pages configuration settings.  Actions are triggered when changes are committed to main.

## Blob Storage

SideSpins uses Azure Blob Storage for storing media files (e.g., videos). Files can be uploaded to blob storage using the `azcopy` command-line tool.

### Uploading Files with azcopy

To copy files from a local directory to blob storage:

```powershell
azcopy copy "C:\Users\JesseFitzGibbon\Videos\pool" `
  "https://jessefitzblob.blob.core.windows.net/videos?sv=2024-11-04&ss=bfqt&srt=sco&sp=rwdlacupiytfx&se=2026-01-15T00:46:25Z&st=2026-01-14T16:31:25Z&spr=https&sig=lEmzKCf%2FqigSt%2FBfoiXWiE8fXIxOKwoSjmm79dyX3rY%3D" `
  --recursive=true `      
  --include-pattern "*.mp4" `
  --overwrite=true
```

**Parameters:**
- First argument: Local source directory path
- Second argument: Azure Blob Storage destination URL with SAS token
- `--recursive=true`: Copy all files in subdirectories
- `--include-pattern "*.mp4"`: Only copy files matching the pattern (e.g., MP4 video files)
- `--overwrite=true`: Overwrite existing files in the destination

**Note:** Replace the SAS token URL with a current token generated from the Azure Portal. SAS tokens have expiration dates.
