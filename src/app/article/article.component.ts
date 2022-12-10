import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';

@Component({
  selector: 'app-article',
  templateUrl: './article.component.html',
  styleUrls: ['./article.component.css']
})
export class ArticleComponent implements OnInit {
   articleId : any;

  constructor( private route: ActivatedRoute) { }  
    ngOnInit(): void {
      this.route.queryParams.subscribe(params => {
        this.articleId = params['articleId'];
      });
    };

}
