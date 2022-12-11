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
 // articleHolder: ArticleInfo = new;

  constructor(private http: HttpClient) {
    //read json data from the articles json directory and push it into the ArticleInfo array
    this.httpService = http;    
  }
  private getFileContent(url: string): Observable<any> {
    return this.http.get(url);
  }  

  public getArticles (){
    if(this.AllArticles.length === 0){
      this.getFileContent(this._jsonURL).subscribe(data => {    
        for(let i=0; i< data.Articles.length; i++){
          this.AllArticles.push(
          new ArticleInfo( data.Articles[i].id, data.Articles[i].title, data.Articles[i].tagline, data.Articles[i].src)
          );
        }     
      });
    }

     return this.AllArticles;
    };

    //  first(isTrue: any) {
    //   return new Promise((resolve, reject) => {
    //     if (isTrue) {
    //       resolve("Promise resolved");
    //     } else {
    //       reject("Promise rejected");
    //     }
    //   });
    // }
  getArticlesHelper()
  {
    return new Promise((resolve, reject) => {
        var success: boolean = false;

        this.getFileContent(this._jsonURL).subscribe(data => {    
          success = true; //TODO:  figure out how to catch error event from subscribe
          for(let i=0; i< data.Articles.length; i++){
            this.AllArticles.push(
            new ArticleInfo( data.Articles[i].id, data.Articles[i].title, data.Articles[i].tagline, data.Articles[i].src)
            );
          }
          
          if (success) {
            resolve(this.AllArticles.length);
          } else {
            reject ("Failed to get JSON article directory content.");
          }

        });
       
      });
    }



  public getArticleContent(aticleTitleToFind: string) : string {
     //TODO:  this will fail if there's not match.   
    var src: string = "";
    var content: string;
    var foundArticle: ArticleInfo;
    
    var success:boolean = false;

    this.getArticlesHelper()
        .then((res) => {
          success = true;
          var result = this.AllArticles.find(item => item.ArticleTitle === aticleTitleToFind); //still matching title with id instead of using an anctual id
          content = result?.ArticleSourceFile;
          console.log(result?.ArticleSourceFile);
          return content;
          //return result?.ArticleTagline;
        }) //response should be a count of articles listed in the directory
        .catch((err) => {
          console.log(err);
          return "Error in Get Article Content"; //err should just say there was a failure.
        });  
        
        return "";
    }
        // var content: any;
  //   if(this.AllArticles.length === 0){
  //     this.getArticlesHelper()
  //       .then((res) => {
  //         success = true;
  //         var result = this.AllArticles.find(item => item.ArticleTitle === aticleTitleToFind); //still matching title with id instead of using an anctual id
  //         content = result?.ArticleSourceFile;
  //         console.log(result?.ArticleSourceFile);
  //         return content;
  //         //return result?.ArticleTagline;
  //       }) //response should be a count of articles listed in the directory
  //       .catch((err) => {
  //         console.log(err);
  //         return "Error in Get Article Content"; //err should just say there was a failure.
  //       });  
  //   }   
  //   else {
  //     var result = this.AllArticles.find(item => item.ArticleTitle === aticleTitleToFind); //still matching title with id instead of using an anctual id
  //     content = result?.ArticleSourceFile;
  //     console.log(result?.ArticleSourceFile);
  //     return content;
  //   }
  //   return "i have no idea how i would get here";
  // }
   
}

export class ArticleInfo {
  ArticleTitle: any;
  ArticleTagline: any;
  ArticleSourceFile: any;
  ArticleId: any;

   constructor(id: string, title: string, tagline: string, src: string){
    this.ArticleId = id;
    this.ArticleTitle = title;
    this.ArticleTagline = tagline;
    this.ArticleSourceFile = src;
   }

}