import { Component } from '@angular/core';
import { ArticlesService } from '../articles.service';
import { SubdomainService } from '../subdomain.service';
import { ActivatedRoute } from '@angular/router';
import { OnInit } from '@angular/core';


@Component({
  selector: 'app-article-presenter',
  templateUrl: './article-presenter.component.html',
  styleUrls: ['./article-presenter.component.css']
})
export class ArticlePresenterComponent implements OnInit {

  markdownContentToPresent = '';
  impersonating = false;
  isLoading = true;

  
  constructor(
    private service: ArticlesService, 
    private route: ActivatedRoute, 
    private subdomainService: SubdomainService) {
   
  }

  ngOnInit() {
    //CHECK THE SUBDOMAIN... IT DRIVES BEHAVIOR.  subdomain is configured at the app service level using cname record in the jessefitz.me DNS zone.
    this.impersonating = this.subdomainService.getSubdomain() == 'not';
    
    //GET CONTENT FROM THE ARTICLE API
    this.route.url.subscribe(segments => {
      // We are defining a unique route for each article.  use the path to query the API for the corresponding article content
      const urlPath = segments[segments.length-1].path;
      // this.content = service.getArticleContent(articleId);
      if(!this.impersonating){
        this.service.getArticleContent(urlPath).subscribe({
          next: (content: string) => {
            // do something with content
            console.log(content);
            let article = JSON.parse(JSON.stringify(content));
            // this.articleContent = ;//
            this.markdownContentToPresent = article.content; 
            this.isLoading = false;
          },
          error: (error: any) => {
            console.error(error);
          },
          complete: () => {
            // do something when the observable completes (optional)
          }
        });
      }
      else  //impersonating
      {
        this.service.getImpersonatedArticleContent(urlPath, "a sentient black hole bent on consuming the universe").subscribe({
          next: (content: string) => {
            // do something with content
            console.log(content);
            // let article = JSON.parse(JSON.stringify(content));
            // this.articleContent = ;//
            this.markdownContentToPresent = content; 
            this.isLoading = false;
          },
          error: (error: any) => {
            console.error(error);
          },
          complete: () => {
            // do something when the observable completes (optional)
          }
        });
      }
  
    });
    
  }

  
}


      /*
      The next property is a callback function that is called each time a new value is emitted by the observable (in this case, when the API response is received). The error property is a callback function that is called if an error occurs during the API request. The complete property is a callback function that is called when the observable completes (which is not relevant in this case, since the observable never completes).
  
      In this example, we pass an object with three arrow functions as properties to the subscribe() method. The next function takes a single argument, content, which is the string returned by the map() operator. The error function takes a single argument, error, which is any error that occurs during the API request.
  
      With this code, you should be able to use the observer syntax with the subscribe() method and avoid the deprecated usage of separate callback functions.
      */
