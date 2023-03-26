import { Component } from '@angular/core';
import { ArticlesService } from '../articles.service';
import { ActivatedRoute } from '@angular/router';
import { OnInit } from '@angular/core';


@Component({
  selector: 'app-article-presenter',
  templateUrl: './article-presenter.component.html',
  styleUrls: ['./article-presenter.component.css']
})
export class ArticlePresenterComponent implements OnInit {

  articleContent = '';
  
  constructor(private service: ArticlesService, private route: ActivatedRoute) {
   
  }

  ngOnInit() {
    this.route.url.subscribe(segments => {
      // We are defining a unique route for each article.  use the path to query the API for the corresponding article content
      const urlPath = segments[0].path;
      // Now you can use the 'hardcodedId' value in your component

      //const articleId = 'b11bf40a-e8f3-4382-9476-bcc947a4ed0e'; // set the article ID here
      // this.content = service.getArticleContent(articleId);
      this.service.getArticleContent(urlPath).subscribe({
        next: (content: string) => {
          // do something with content
          console.log(content);
          let article = JSON.parse(JSON.stringify(content));
          // this.articleContent = ;//
          this.articleContent = article.content; 
        },
        error: (error: any) => {
          console.error(error);
        },
        complete: () => {
          // do something when the observable completes (optional)
        }
      });
  
    });
    
  }
}


      /*
      The next property is a callback function that is called each time a new value is emitted by the observable (in this case, when the API response is received). The error property is a callback function that is called if an error occurs during the API request. The complete property is a callback function that is called when the observable completes (which is not relevant in this case, since the observable never completes).
  
      In this example, we pass an object with three arrow functions as properties to the subscribe() method. The next function takes a single argument, content, which is the string returned by the map() operator. The error function takes a single argument, error, which is any error that occurs during the API request.
  
      With this code, you should be able to use the observer syntax with the subscribe() method and avoid the deprecated usage of separate callback functions.
      */
