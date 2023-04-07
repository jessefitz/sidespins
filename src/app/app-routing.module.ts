import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
import { SandboxComponent } from './sandbox/sandbox.component';
import { ArticlePresenterComponent } from './article-presenter/article-presenter.component';
import { AboutComponent } from './about/about.component';
import { AiUsageComponent } from './articles/ai-usage/ai-usage.component';

/*--BEGIN ROUTES--*/
const routes: Routes = [
  {path: 'articles/a-place-problem', component:ArticlePresenterComponent},
  {path: 'articles/hope-and-validation', component:ArticlePresenterComponent},
  {path: 'articles/not-me', component:ArticlePresenterComponent},
  {path: 'articles/friends-and-enemies', component:ArticlePresenterComponent},
  {path: 'articles/no-shame', component:ArticlePresenterComponent},
  {path: 'articles/aloof', component:ArticlePresenterComponent},
  {path: 'articles/noncompete-agreements', component:ArticlePresenterComponent},
  {path: 'articles/alone-together', component:ArticlePresenterComponent},
  {path: 'articles/vanity', component:ArticlePresenterComponent},
  {path: 'articles/falling-in-love', component:ArticlePresenterComponent},
  {path: 'articles/a-dangerous-race', component:ArticlePresenterComponent},
  {path: 'articles/rework', component:ArticlePresenterComponent},
  {path: 'articles/ai-usage', component:ArticlePresenterComponent},
  {path: 'articles/mondays', component:ArticlePresenterComponent},
  {path: 'articles/revised-authoring-process', component:ArticlePresenterComponent},
  {path: 'articles/automation', component:ArticlePresenterComponent},
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  {path: 'about', component:AboutComponent},
  {path: 'ai', component:AiUsageComponent},
  {path: 'articles/chatgpt', component:ArticlePresenterComponent},  
  {path: 'articles/how-it-works', component:ArticlePresenterComponent},  
  {path: 'articles/website-infrastructure', component:ArticlePresenterComponent},  
  {path: 'articles/introducing-frankenstein', component:ArticlePresenterComponent},   
  {path: 'articles/more-on-motivation', component:ArticlePresenterComponent},
  {path: 'articles/schooled-by-chatgpt', component:ArticlePresenterComponent},
  {path: 'articles/bridge', component:ArticlePresenterComponent},
  {path: 'sandbox', component: SandboxComponent},
  {path: 'articles/the-old-mill-town', component: ArticlePresenterComponent}

];
/*--END ROUTES--*/

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    initialNavigation: 'enabledBlocking'
})],
  exports: [RouterModule]

})
export class AppRoutingModule { }
