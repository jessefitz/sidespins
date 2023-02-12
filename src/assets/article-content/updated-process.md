# Updated Process

<p align="center">
    <img src="/img/process.jpg">
</p>

1. Ensure workspace is configured for a branch other than prod in the jessefitz_app repo.
2. Create a markdown file in the /assets/article-content directory. 
   1. If including an image, add the image (in jpg format) to the /img directory.
   2. Reference the image using inline HTML or the appropriate  Markdown notation
        ```
        <p align="center">
            <img src="/img/bridge.jpg">
        </p>
        ```
        or
        ```
        ![Diagram]([/path/to/file])
        ```
3. Run the article registration script using the appropriate parameter values.
     - markdownFile: the filename for the newly authored article, ex. my-new-article.md
     - componentName: the name to use for the new angular component.  This value isn't forward facing.
     - articlePath: the path to use for the new route.  This is the URL used in the browser.
     - displayTitle: the title for the article on the articles listing page
  
    Example Usage:
    ```
        .\src\scripts\new-jfitzarticle.ps1 -componentName "newArticle" -articlePath "new-article" -markdownFile "new-article.md" -displayTitle "My New Article"
    ```
4. Test changes by building and running the site locally.
   ```
   ng build
   ng serve --open
   ```
5. Commit all changes and push to GitHub
6. Create and approve a pull request from the dev to prod branch.