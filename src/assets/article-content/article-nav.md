# Enhancing Article Navigation

I've been passively using the site for a couple of months now, primarily for authoring of "articles".  As the collection of content grows I see an increasing need to provide a better way to navigate the article list.

## Crawl
- A basic "internal" sorting/filtering capability that will allow the author to control what renders in the article list and in what order it renders.
- A mechanism to categorize articles and to have the list render articles grouped by category.
  
## Walk
- End user capability to sort/filter articles by category
  
## Run
- A visually pleasing end-user experience

## Constraints
- The site will remain constrained by Azure Static Web capabilities.  I have no immediate plans to include dynamic data or other backend integrations.
- If possible, avoid using new 3rd party components or packages.  Do what's possible with Angular, Bootstrap, and Frankenstein's inner workings.

## To Dos

### Crawl
- Add new attributes into existing entries in article json directory ☑️
- Update the article-list.component.html file to understand and render according to those attributes
- Update article registration script to accept and use parameters for tags and sorting.