import { Component, OnInit } from '@angular/core';
import { ArticleInfo, ArticlesService } from '../articles.service';

@Component({
  selector: 'app-article-list',
  templateUrl: './article-list.component.html',
  styleUrls: ['./article-list.component.css'],
  providers: [ArticlesService]
})

export class ArticleListComponent implements OnInit {
  title = 'Read JSON File with Angular 15';
  foo = 'foo';
 
  articles: ArticleInfo[] = [];

  constructor( private service: ArticlesService) { }  
    ngOnInit(): void {
      this.articles = this.service.AllArticles;
    };
  }


 

