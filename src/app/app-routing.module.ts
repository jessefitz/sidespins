import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
// import { ArticleComponent } from './article/article.component';
import { HelloVenusComponent } from './articles/hello-venus/hello-venus.component';
import { IntroducingFrankensteinComponent } from './articles/introducing-frankenstein/introducing-frankenstein.component';
import { ChatGptComponent } from './articles/chat-gpt/chat-gpt.component';
import { HowItWorksComponent } from './articles/how-it-works/how-it-works.component';
import { WebsiteInfrastructureComponent } from './articles/website-infrastructure/website-infrastructure.component';
import { MoreOnMotivationComponent } from './articles/more-on-motivation/more-on-motivation.component';
import { SchooledByChatgptComponent } from './articles/schooled-by-chatgpt/schooled-by-chatgpt.component';

const routes: Routes = [
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  // {path: 'article', component:ArticleComponent},
  // {path: 'article/:articleUrlPath', component:ArticleComponent},
  //Frankenstein needs a route defined per article.  //register the same path as urlpath in the articlesdirectory.json file.
  {path: 'articles/venus', component:HelloVenusComponent},  
  {path: 'articles/chatgpt', component:ChatGptComponent},  
  {path: 'articles/how-it-works', component:HowItWorksComponent},  
  {path: 'articles/website-infrastructure', component:WebsiteInfrastructureComponent},  
  {path: 'articles/introducing-frankenstein', component:IntroducingFrankensteinComponent},   
  {path: 'articles/more-on-motivation', component:MoreOnMotivationComponent},
  {path: 'articles/schooled-by-chatgpt', component:SchooledByChatgptComponent}  
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    initialNavigation: 'enabledBlocking'
})],
  exports: [RouterModule]

})
export class AppRoutingModule { }
