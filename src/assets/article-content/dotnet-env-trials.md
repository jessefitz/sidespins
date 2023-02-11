# Trials and Tribulations with Azure Func

Caveat to all of this... this effort was pursued while I was sick with the 'rona.

I used this article to guide me:
https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-cli-csharp?tabs=azure-cli%2Cin-process

What a pain in the ass this has been.  I've probably spent 6+ hours going in circles what I thought was SDK versioning issues.  These issues appear to have been related to two primary issues with getting my .NET dev environment configured properly...

## The SDK Environment Path Was Wrong

For some reason when I looked in the system path settings there was a variable in there for the x86 version of .net sdk.  When I moved the x64 version up above it, things started improving.

## The Nuget sources weren't set up.

This was resolved with 
	dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org 

##Commands to Remember

starts the function locally
func start

publishes the app to the (already created) function app in Azure
 func azure functionapp publish jessefitzapi      

pulls app settings to assist with connection string to storage account
func azure functionapp fetch-app-settings jessefitzapi 


#Moving on to the SMS side...

to get text messages coming into storage queue.  turns out (facepalm) i didn't even need the function, though there is an event grid hook for azure functions.
https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/sms/handle-sms-events#about-event-grid

to experiment with sending sms (this was too easy)
https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/sms/send?tabs=windows&pivots=platform-azcli


