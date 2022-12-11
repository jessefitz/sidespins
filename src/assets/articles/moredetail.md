# Why am I doing this...

To start with, I just wanted to create a simple static site that I could post some content on.  As of the day I'm writing this article, I have that, but I've made it more complicated for myself :)

## More detail....

For my next trick, I'll write some more detailed aricles explaining what I did and how I did it.  

But here's the shorter version of the long story is:

- This site is hosted in an Azure Static Website service instance.
- I built a very basic Angular app with bootstrap to render the primary elements of the web site pages, but...
- I also built a Frankenstein CMS that allows me to maintain a collection of articles as Markdown in a GitHub repo.  
    - The CMS directory of articles is maintained by a JSON file in the repo.
    - Each article page uses the 3rd party ngx-markdown library to render article content that I store as individual markdown files in my repo.


All in all, the most difficult thing about this entire exercise has been taking my theoretical knowledge of Angular and Bootstrap and applying them in a for realz situation.  It took me about a weekend to get it down.