# Reference for Authoring New Articles 

This process is what I've come up with so that my full set of articles can be hydrated using SSR, which should improve SEO and linkability.  Without explicitly defining components with links to the MD content, the SSR hydration process doesn't know what to generate.

1. Create the new article in assets/articles directory.
2. ng generate component articles/[*name of article*] --module=app.module --inline-template --inline-style --skip-import --skip-tests
3. in template that's created, update template to template: `<markdown src="assets/articles/chatgpt.md"></markdown>`,
4.  in app-routing.module.ts, 
    1.  add import { HelloVenusComponent } from './articles/hello-venus/hello-venus.component';
    2.  add a route {path: 'articles/venus', component:HelloVenusComponent}

This much has been tested locally and is working.

## To Do's:
- document and test steps for adding this into the articles directory JSON to support linking from within the articles directory page
- move on to test whether or not any of this will actually work in static web apps by following the steps to add the build command in the git yaml file.
- clean up all the extra components i added during testing.