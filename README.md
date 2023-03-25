# JessefitzApp

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 15.0.2.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## CMS vNext Notes

### Plan
- Create a new component that reads ID from HTTP Request and gets content from new service, templated <markdown>{{content}}</markdown>
- Update ArticlesService to call the new API to get content
  - Update the ArticleInfo object to include the content variable, then return the full set of shit
- Create a new API method that calls cosmos to get content
  - Use local.settings.json to config connections

If all that works...
- Create new routes for articles by ID
- Update ArticleList service to render required links in desired format
- Create new build event that moves article MD file contents into cosmos

If all that works...
- Create new GitHub action(s) that copy contents of repo article files into cosmos
  

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
