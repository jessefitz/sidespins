import { Component, OnInit } from '@angular/core';
import { ArticleInfo, ArticlesService } from '../articles.service';

@Component({
  selector: 'app-article-list',
  templateUrl: './article-list.component.html',
  styleUrls: ['./article-list.component.css'],
  providers: [ArticlesService],
  
})

export class ArticleListComponent implements OnInit {
  title = 'Read JSON File with Angular 15';
  foo = 'foo';
 
  articles: ArticleInfo[] = [];
  categories: string[] = [];

  constructor( private service: ArticlesService) { }  
    ngOnInit(): void {
      this.articles = this.service.getArticlesList();     
      console.log (this.categories.length);
    };

    filteredItems(category: string): any[] {
      return this.articles
        .filter(item => item.ArticleCategory === category)
        .sort((a:ArticleInfo, b:ArticleInfo) => new Date(b.ArticleDate).getTime() - new Date(a.ArticleDate).getTime())
        .sort((a:ArticleInfo, b:ArticleInfo) => a.ArticleRank - b.ArticleRank);
    };

    getCategories(): any[] {
         /**ChatGPT Guidance
       * To build a new array of unique categories, we use the map() method to extract the category property 
       * from each object in the sourceObjects array. We then create a new Set object from this array, 
       * which automatically removes any duplicates. Finally, we use the spread syntax ... 
       * to convert the Set back into an array and assign it to the uniqueCategories variable.**/
      this.categories  = [...new Set(this.articles.map(obj => obj.ArticleCategory))];

      //hack to sort by my preferred category rendering order.
      //TODO: eventually create a new object responsible for categories that also gives the ability to define sort order
      this.categories = ["Tech and Biz", "Prose", "Frankenstein"];

      return this.categories;
    };
  }


 

