import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { ArticleInfo, ArticlesService } from '../articles.service';
import { HttpClient } from '@angular/common/http';
import { JsonPipe } from '@angular/common';

@Component({
  selector: 'app-article',
  templateUrl: './article.component.html',
  styleUrls: ['./article.component.css'],
  providers: [ArticlesService]
})
export class ArticleComponent implements OnInit {
   articleUrlPath : any;
   articleContent: any;
  //  articlesServce: any;
   AllArticles : ArticleInfo[] = [];
   data = [];
   articleSrc: string = "";
   articleTagline: string = "";
   articleContentAsync: Promise<string>|null = null;

   private resolve: Function|null = null;
  


   getMyArticleContentPromise(articleTitleToFind: string): Promise<string> {
    return new Promise((resolve, reject) => {
      //setTimeout(() => resolve(articleTitleToFind), 3000);  //this would be where i call my function to pull article content ansync?
      //resolve(this.service.getArticleContent(articleTitleToFind));  //this needs to be in resolve?
      resolve(this.service.getArticleContent(articleTitleToFind)); 
    });
  }

  constructor(private http: HttpClient, private service: ArticlesService, private route: ActivatedRoute) { }  
    ngOnInit() {
      // this.articlesServce = this.service;
      this.articleUrlPath = this.route.snapshot.paramMap.get('articleUrlPath');
      // this.articleContentAsync = this.getMyArticleContentPromise(this.articleTitle);
      this.getArticleInfo(this.articleUrlPath);

    }
  

  getArticleInfo(articleTitleToFind : string){
    var success: boolean = false;
    //using a promise here appears to mitigate the issues I was experiencing with async property values.
    //i've eliminted usage of the ArticlesService until I can prioritize time to clean things up.
    const promise = new Promise<void>((resolve, reject) => {
      // const apiURL = this.api;
      this.http.get('assets/article-directory.json').subscribe(data => {    
        success = true; //TODO:  figure out how to catch error event from subscribe
              this.articleContent = "it resolved";;
              var results = (JSON.parse(JSON.stringify(data)));

              for(let i=0; i< results.Articles.length; i++){
                
                if (results.Articles[i].urlpath === articleTitleToFind){
                  this.articleTagline = results.Articles[i].tagline; 
                  this.articleSrc = results.Articles[i].src;
                  break;
                }
              }  
              
        if (success) {
          resolve();
        } else {
          reject ("Failed to get JSON article directory content.");
        }

      });
    });
    return promise;
  }
}
