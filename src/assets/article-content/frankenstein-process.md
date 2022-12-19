# Reference for Authoring New Articles 

This process is what I've come up with so that my full set of articles can be hydrated using SSR, which should improve SEO and linkability.  Without explicitly defining components with links to the MD content, the SSR hydration process doesn't know what to generate.

1. Create the new article in assets/articles directory.
2. ng generate component articles/[*name of article*] --module=app.module --inline-template --inline-style --skip-tests
3. in template that's created, update template to template: `<markdown src="assets/articles/chatgpt.md"></markdown>`,
4.  in app-routing.module.ts, 
    1.  add import { HelloVenusComponent } from './articles/hello-venus/hello-venus.component';
    2.  add a route {path: 'articles/venus', component:HelloVenusComponent}

This much has been tested locally and is functioning in "production".

## To Do's:
- I'd like to automate this entire process.  Could take the form of a script that I run manually, or a file system watcher that does all this when a new article is added into the article-content directory.
- Client browsers will cache most/all of this static content.  There's got to be a version parameter or something I can put in the index.html that, if changed, will force the client to replace files that have been previously cached.  Maybe I should ask ChatGPT.
  
  # Load the JSON file into a variable
$data = Get-Content -Raw -Path data.json | ConvertFrom-Json

# Add a new object to the array
$data += @{ name = 'John'; age = 30; city = 'New York' }

# Write the modified array back to the JSON file
$data | ConvertTo-Json | Set-Content -Path data.json
