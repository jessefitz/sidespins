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
