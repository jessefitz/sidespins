import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { of } from 'rxjs';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { TransferState, makeStateKey } from '@angular/platform-browser';
// import axios from 'axios';


@Injectable({
  providedIn: 'root'
})


export class ArticlesService {  
  
  AllArticles : ArticleInfo[] = [];
  subscription: any;
  httpService: HttpClient;  
  private _jsonURL = 'assets/article-directory.json';


  constructor(private http: HttpClient, private transferState: TransferState) {
    this.httpService = http;    

    /*  To fetch data from the API on the server side, you can use Angular's HttpClient in combination with the TransferState service. 
    This will allow you to make API calls on the server, and then transfer the fetched data to the client side. */


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

  //in its current implementation, this function returns a json string representation of the article item in cosmos.
  public getArticleContent(urlPath: any): Observable<string> {
    
   const articleKey = makeStateKey<any>(urlPath);
   const articleContent = this.transferState.get(articleKey, null);
  
   if( articleContent){
     return of(articleContent); // The of() function is a creation operator that creates an Observable emitting the provided values.
   }
   else
   {
    return this.http.get(`/api/GetArticleContent?urlPath=${urlPath}`)
      .pipe(
        map((response: any) => {
          const valToReturn = response && response.length ? response[0] : 'No matching article found';
          this.transferState.set(articleKey, valToReturn);
          return valToReturn;
        })
      );
   }

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
  
  // public async getImpersonatedArticleContent(urlPath: any, persona: string): Promise<string> {
  //   const encodedPersona = encodeURIComponent(persona);
  //   try {
  //     const response = await axios.get(`/api/GetImpersonatedContent?articlePath=${urlPath}&persona=${encodedPersona}`);
  //     const valToReturn = response && response.data.length ? response.data : 'No matching article found';
  //     return valToReturn;
  //   } catch (error) {
  //     console.error(error);
  //     return 'Error retrieving article';
  //   }
  // }

  public getImpersonatedArticleContent(urlPath: any, persona: string): Observable<string> {
    const encodedPersona = encodeURIComponent(persona);
    return this.http.get(`/api/GetImpersonatedContent?articlePath=${urlPath}&persona=${encodedPersona}`)
      .pipe(
        map((response: any) => {
          const valToReturn = (response != null) ? response.content : "Couldn't impersonate content.";
          return valToReturn;
        })
      );
    
  }

  // public async getImpersonatedArticleContent(urlPath: any, persona: string) {
  //   ///api/GetArticleContent?urlPath=${urlPath}
  //   let impersonatedArticleContent = '';
  //   let actualArticleContent = '';
    
  //   //first get the article content
  //   const requestOptions = {
  //     method: 'GET',
  //     url: 'api/GetArticleContent?urlPath='+urlPath,
  //   }

  //   try {
  //     const axios = require('axios');
  //     let response = await axios(requestOptions);
  //     actualArticleContent = response;


  //     const requestOptionsForOpenAI = {
  //       method: 'GET',
  //       url: 'api/GetImpersonatedContent?persona='+persona,
  //     }
  //     //got the original content... now need to make another API call to get the altered content.
  //       let responseFromOpenAI = aw

  //   } catch (error) {
  //     impersonatedArticleContent = "Error: " + error.message;
  //   }

  // };

    
  

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