import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
// import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
// import { ArticleComponent } from './article/article.component';
import { HelloVenusComponent } from './articles/hello-venus/hello-venus.component';

const routes: Routes = [
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  // {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  // {path: 'article', component:ArticleComponent},
  // {path: 'article/:articleUrlPath', component:ArticleComponent},
  {path: 'articles/venus', component:HelloVenusComponent}
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    initialNavigation: 'enabledBlocking'
})],
  exports: [RouterModule]

})
export class AppRoutingModule { }
