# JessefitzApp

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 15.0.2.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## CMS vNext Notes

### Plan
- [DONE] Create a new component that reads ID from HTTP Request and gets content from new service, templated <markdown>{{content}}</markdown>
- [DONE] Update ArticlesService to call the new API to get content
  - Update the ArticleInfo object to include the content variable, then return the full set of shit
- [DONE] Create a new API method that calls cosmos to get content
  - Use local.settings.json to config connections

If all that works...
- [Next2] Create new routes for articles by ID
- Update ArticleList service to render required links in desired format
- [Next1] Create new build event that moves article MD file contents into cosmos
  - [TODO]  
    - The build event needs to call the console app
    - [Done] The console app needs to drop the items in the cosmos container, then repopulate by iterating through the articles directory json file, to include the urlpath attribute
    - [Done] The article directory json file needs to be updated to include the article filepath.
      - [Done] Use the SRC attribute that's already there, and just make a hard-coded constant in the migration utility for the full path.
    - [Done] Ipdate ArticleService to lookup cosmos using the urlpath attribute can be used as a lookup in the cosmos from the article presenter
    - Add paths in the angular routes that match the URL path but that all go to the presenter
    - Need to add all the new config variables to app settings
    - The new "register new article" process is now going to consist of:
      - Adding the new entry to article directory
      - No longer generating new ang component
    - Update article list page to render appropriate links (maybe)

### not.jessefitz.me
- [DONE] Create and test the not.jessefitz.me domain in prod
  - to do this locally, you have to create a  not.localhost entry in hosts file.
- [Done] Create the subdomain service and check for presence of the 'not' identifier
- Inject it into the ArticlePresenter and implement conditional logic
  - Get MD content
  - Make secondary API call to OpenAI to "translate" for persona
- Create new API endpoint to ChatGPT that accepts MD content and persona parameter

If all that works...
- Create new GitHub action(s) that copy contents of repo article files into cosmos
  
### Migrate Content
dotnet build --configuration Debug --debug --output bin/Debug/net6.0/

### 3/22/23 Update
Added node API.  To run/debug locally, use SWA CLI. Was able to get this working according to most widely [available documentation on web](https://learn.microsoft.com/en-gb/azure/static-web-apps/add-api?tabs=vanilla-javascript), with the exception of the following items:
- SWA doesn't work with latest version of Node, so I installed [Windows Node Version Manager](https://github.com/coreybutler/nvm-windows) and switched to Node 16.16.0.
- Running SWA build command seemed to work, but SWA start was looking for API on the wrong port.  This started working after I added proxy.conf.json to point to the local port app is running on.  Also needed to update start command in package.json ""start": "ng serve --proxy-config proxy.conf.json".
- Debugging:  Getting the debugger to attach using the SWA tools was and still is a bit of a mystery, but locally able to debug by running the SWA: Run jessefitz_app debug config.  Make sure to set Toggle Auto Attach to 'Smart'.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.
