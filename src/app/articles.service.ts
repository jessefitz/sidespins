import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';


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
  public getArticlesList (){
    if(this.AllArticles.length === 0){
      //subscribe is necessary due to async nature of reading file content.
      this.getFileContent(this._jsonURL).subscribe(data => {    
        for(let i=0; i< data.length; i++){
          this.AllArticles.push(
            new ArticleInfo( data[i].id, data[i].title, data[i].tagline, data[i].src, 
              data[i].urlpath, data[i].category, data[i].rank, data[i].date)
          );
        }     
      });
    }

     return this.AllArticles;
    };
     
  // public getArticleContent(id: any){

  //   let content = '';

  //   this.http.get(`/api/GetArticleContent?ArticleId=${id}`)
  //   .subscribe((resp: any) => {
  //     console.log(resp); // log the resp object to the console
  //     content = resp.message;
  //   });

  //   return content;
  // }  

  //in its current implementation, this function returns a json string representation of the article item in cosmos.
  public getArticleContent(id: any): Observable<string> {
    return this.http.get(`/api/GetArticleContent?ArticleId=${id}`)
      .pipe(
        map((response: any) => {
          const valToReturn = response && response.length ? response[0] : 'No matching article found';
          return valToReturn;
        })
      );

     /*
      The getArticleContent() method takes an id parameter and returns an observable of type string.

      The http.get() method sends an HTTP GET request to the GetArticleContent API with the specified id parameter, and returns an observable of the HTTP response.

      The pipe() method is called on the observable returned by http.get() to apply one or more operators to the emitted values of the observable.

      The map() operator is passed as an argument to pipe() to apply the map() operator to the emitted values of the observable. The map() operator takes a callback function that is called for each emitted value of the observable.

      The callback function for map() takes the HTTP response object as an argument (resp: any) and returns the message property of the object as a string.

      The map() operator returns a new observable that emits the transformed values (in this case, the message property as a string).

      The observable returned by pipe() (and getArticleContent()) is of type Observable<string>, indicating that it emits strings.
      */
  }

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
  ArticleDate: any;

   constructor(id: string, title: string, tagline: string, src: string, url: string, category: string, rank: string, date: string){
    this.ArticleId = id;
    this.ArticleTitle = title;
    this.ArticleTagline = tagline;
    this.ArticleSourceFile = src;
    this.ArticleUrlPath = url;
    this.ArticleCategory = category;
    this.ArticleRank = rank;
    this.ArticleDate = date;
   }

}