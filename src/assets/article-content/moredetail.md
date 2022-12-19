# Why am I doing this...

To start with, I just wanted to create a simple static site that I could post some content on.  I started  by looking at the standard web hosting providers and then I remembered I'm cheap and don't like paying others for things that I can do on my own.  Also, I was looking for a project that I could use to teach my son about some basic programming and cloud technology concepts.

So.. as of the day I'm writing this article, I think I've accomplished a baseline for both goals.

## More detail....

Here's the short version of a long story:

- This site is hosted in an Azure Static Web App instance.
- I built a very basic Angular app with Bootstrap to render the primary elements of the web site pages, but...
- I also built a Frankenstein CMS that allows me to maintain a collection of articles as Markdown in a GitHub repo.  
    - The CMS directory of articles is maintained by a JSON file in the repo.
    - Each article page uses the 3rd party ngx-markdown library to render article content that I store as individual markdown files in my repo.


All in all, the most difficult thing about this entire exercise has been taking my theoretical knowledge of Angular and Bootstrap and applying them in a for realz situation.  It took me about a weekend to get it down.

I've got a backlog of additional things I want to do with the site, but that same backlog is already brimming with other work, school and personal projects.

## Things to Think About

404's are, I guess, just an expected behavior when refreshing Angular pages since there isn't *really* a page to render at any of the given URLs.

There appear to be two paths to choose from:
   - A [workaround](https://medium.com/wineofbits/angular-2-routing-404-page-not-found-on-refresh-a9a0f5786268) that at least forces to full URLs to work
   - Server Side Rendering (SSR) that actually creates pages that can be crawled, bookmarked, etc.   There are some [unique considerations](https://dotnetthoughts.net/angular-server-side-rendering-azure-static-webapps/) for SSR in Azure Static Web Apps that I've read about here.
