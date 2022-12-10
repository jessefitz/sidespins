import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
import { ArticleComponent } from './article/article.component';

const routes: Routes = [
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  {path: 'article', component:ArticleComponent},
  {path: 'article/*', component:ArticleComponent}
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]

})
export class AppRoutingModule { }
