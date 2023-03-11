import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { ResolveEnd } from '@angular/router';


@Injectable({
  providedIn: 'root'
})


export class ArticlesService {  
  
  AllArticles : ArticleInfo[] = [];
  subscription: any;
  httpService: HttpClient;
  
  private _jsonURL = 'assets/article-directory.json';


  constructor(private http: HttpClient) {
    //read json data from the articles json directory and push it into the ArticleInfo array
    this.httpService = http;    
  }
  public getArticles (){
    if(this.AllArticles.length === 0){
      //subscribe is necessary due to async nature of reading file content.
      this.getFileContent(this._jsonURL).subscribe(data => {    
        for(let i=0; i< data.length; i++){
          this.AllArticles.push(
            new ArticleInfo( data[i].id, data[i].title, data[i].tagline, data[i].src, 
              data[i].urlpath, data[i].category, data[i].rank)
          );
        }     
      });
    }

     return this.AllArticles;
    };
     
    
  private getFileContent(url: string): Observable<any> {
    return this.http.get(url);
  }  

}

export class ArticleInfo {
  ArticleTitle: any;
  ArticleTagline: any;
  ArticleSourceFile: any;
  ArticleId: any;
  ArticleUrlPath: any;
  ArticleCategory: any;
  ArticleRank: any;

   constructor(id: string, title: string, tagline: string, src: string, url: string, category: string, rank: string){
    this.ArticleId = id;
    this.ArticleTitle = title;
    this.ArticleTagline = tagline;
    this.ArticleSourceFile = src;
    this.ArticleUrlPath = url;
    this.ArticleCategory = category;
    this.ArticleRank = rank;
   }

}