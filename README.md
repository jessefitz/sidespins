# JessefitzApp

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 15.0.2.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Decoupling Content from Source

### Goals
- Streamline the process for getting new content into the website
- Decouple code changes from content changes
- Don't lose the backup/history of markdown and images... this is the content that should be transient across future platforms

### Areas
- Change content migration tool to include all metadata in article directory
- Get rid of article-directory.json... make everything data driven by content in Cosmos.
  - Decouple article ArticleService.getArticleList from article-directory.json
    - Make a call to the existing API endpoint for getting article by path in the API
    - Change the logic in that API endpoint where if path parameter is empty, it returns all articles
      - Future improvement: cache all article content when this call is made to improve site performance
- Build a secure content management area in the site that only I can access
  - https://www.npmjs.com/package/ngx-markdown-editor
- Better understand what you want to do with backups...  According to [MS](https://learn.microsoft.com/en-us/azure/cosmos-db/periodic-backup-restore-introduction) "You can't access this backup directly. Azure Cosmos DB team restores your backup when you request through a support request."

### Phases
1. Phase 1
   1.  Decouple ArticleService from Article Directory
   2.  Couple ArticleService with Cosmos 
   3.  Put all article metadata in migrate utility
   4.  At this point, site should be able to receive new articles without needing a release.... 

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

# Future Considerations

## Improve Performance with Blob and CDN
Azure Content Delivery Network (CDN) and Azure Blob Storage serve different purposes, but they can be used together to deliver an enhanced experience for serving content to users. Here are some benefits of using Azure CDN over just using Blob Storage:

Global distribution: Azure CDN has a network of edge servers located across the globe. When you use Azure CDN to serve content, it is automatically cached at these edge servers. This allows users to access the content from the server closest to their location, reducing latency and improving load times.

Improved performance: Azure CDN uses various optimization techniques such as compression, caching, and connection reuse to ensure faster content delivery. These optimizations help reduce the load times of your content, improving the user experience.

Scalability: Azure CDN can handle large amounts of traffic and is designed to scale with your needs. As your traffic grows, the CDN automatically scales to accommodate the increased load. This means you don't have to worry about provisioning and managing additional resources to handle traffic spikes.

Reduced load on origin: By caching and serving content from edge servers, Azure CDN reduces the load on your origin server (e.g., Blob Storage). This can help save bandwidth and improve the overall performance of your application.

Security: Azure CDN offers features such as custom domain HTTPS, token-based authentication, and DDoS protection to help secure your content and protect your infrastructure from attacks.

Analytics and monitoring: Azure CDN provides built-in analytics and monitoring capabilities, giving you insights into user activity, traffic patterns, and performance metrics.

It is important to note that Azure CDN and Blob Storage are not mutually exclusive solutions. You can use Azure Blob Storage as your origin server to store content, and then use Azure CDN to deliver that content to users with lower latency and improved performance. This combination can help you achieve the best of both worlds: a scalable, cost-effective storage solution and a high-performance content delivery network.
## SEO
Check your server-side rendering setup: Ensure that your Angular application is correctly configured for SSR. This allows search engines to crawl and index your content more effectively. Review your Angular Universal setup and make sure it's working as expected.

Create a sitemap: Generate a sitemap.xml file that lists all the pages on your website. This helps search engines discover and index your content more effectively. You can submit the sitemap to Google Search Console and Bing Webmaster Tools.

Register with Google Search Console and Bing Webmaster Tools: Register your website with these tools to monitor your site's performance, get insights, and submit your sitemap. They also provide valuable information on any crawl errors or indexing issues.

Optimize metadata: Ensure that your pages have unique and descriptive title tags and meta descriptions. This helps search engines understand your content better and display relevant snippets in search results.

Use semantic HTML tags: Use appropriate HTML tags like <header>, <nav>, <main>, and <footer> to structure your content. This helps search engines understand the structure of your website and index it accordingly.

Use descriptive URLs: Use clear, descriptive URLs that include relevant keywords. This makes it easier for search engines to understand your content and improves your SEO ranking.

Improve page load speed: Optimize your website's performance by compressing images, minifying JavaScript and CSS files, and using caching techniques. Faster load times can lead to better rankings in search results.

Add structured data: Use structured data markup (such as JSON-LD or schema.org) to provide search engines with additional information about your content. This can improve your visibility in search results and increase click-through rates.

Build quality backlinks: Earn backlinks from reputable websites by creating valuable content and promoting it through social media, guest posting, or outreach. High-quality backlinks can improve your website's authority and search engine ranking.

Monitor your progress: Regularly check your website's performance in Google Search Console and Bing Webmaster Tools. Address any issues and continue to optimize your content for better visibility in search results.