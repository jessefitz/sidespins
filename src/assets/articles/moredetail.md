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
