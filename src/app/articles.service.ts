import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})


export class ArticlesService {  
  
  AllArticles : ArticleInfo[] = [];
  
  private _jsonURL = 'assets/article-directory.json';
  constructor(private http: HttpClient) {
    this.getJSON().subscribe(data => {
    //  this.stuff= data.Articles;
     for(let i=0; i< data.Articles.length; i++){
       this.AllArticles.push(
        new ArticleInfo( data.Articles[i].title, data.Articles[i].tagline, data.Articles[i].src)
       );
      }     
    });  
    
  }
  public getJSON(): Observable<any> {
    return this.http.get(this._jsonURL);
  }  
}

export class ArticleInfo {
  ArticleTitle: any;
  ArticleTagline: any;
  ArticleSourceFile: any;
   constructor(title: string, tagline: string, src: string){
    this.ArticleTitle = title;
    this.ArticleTagline = tagline;
    this.ArticleSourceFile = src;
   }
}

// interface article{
//   title: string;
//   src: string;
//   tagline: string;
//   date: string;
// }