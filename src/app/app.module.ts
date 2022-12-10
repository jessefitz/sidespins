import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { MarkdownModule } from 'ngx-markdown';
import { HttpClientModule, HttpClient } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { ArticleListComponentComponent } from './article-list-component/article-list-component.component';

@NgModule({
  declarations: [
    AppComponent,
    ArticleListComponentComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HttpClientModule,
    MarkdownModule.forRoot({ loader: HttpClient }), 
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
