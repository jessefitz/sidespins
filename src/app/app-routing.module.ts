import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
import { UpdatedAuthoringProcessComponent } from './articles/updated-authoring-process/updated-authoring-process.component';
import { AutomationComponent } from './articles/automation/automation.component';
import { IntroducingFrankensteinComponent } from './articles/introducing-frankenstein/introducing-frankenstein.component';
import { ChatGptComponent } from './articles/chat-gpt/chat-gpt.component';
import { HowItWorksComponent } from './articles/how-it-works/how-it-works.component';
import { WebsiteInfrastructureComponent } from './articles/website-infrastructure/website-infrastructure.component';
import { MoreOnMotivationComponent } from './articles/more-on-motivation/more-on-motivation.component';
import { SchooledByChatgptComponent } from './articles/schooled-by-chatgpt/schooled-by-chatgpt.component';
import { BridgeComponent } from './articles/bridge/bridge.component';


/*--BEGIN ROUTES--*/
const routes: Routes = [
  {path: 'articles/revised-authoring-process', component:UpdatedAuthoringProcessComponent},
  {path: 'articles/automation', component:AutomationComponent},
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  //Frankenstein needs a route defined per article.  //register the same path as urlpath in the articlesdirectory.json file.
  {path: 'articles/chatgpt', component:ChatGptComponent},  
  {path: 'articles/how-it-works', component:HowItWorksComponent},  
  {path: 'articles/website-infrastructure', component:WebsiteInfrastructureComponent},  
  {path: 'articles/introducing-frankenstein', component:IntroducingFrankensteinComponent},   
  {path: 'articles/more-on-motivation', component:MoreOnMotivationComponent},
  {path: 'articles/schooled-by-chatgpt', component:SchooledByChatgptComponent},
  {path: 'articles/bridge', component:BridgeComponent}  
];
/*--END ROUTES--*/

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    initialNavigation: 'enabledBlocking'
})],
  exports: [RouterModule]

})
export class AppRoutingModule { }
