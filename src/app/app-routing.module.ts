import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
import { MillTownComponent } from './articles/mill-town/mill-town.component';
import { FriendsComponent } from './articles/friends/friends.component';
import { NoShameComponent } from './articles/no-shame/no-shame.component';
import { AloofComponent } from './articles/aloof/aloof.component';
import { NoncompeteAgreementsComponent } from './articles/noncompete-agreements/noncompete-agreements.component';
import { AloneTogetherComponent } from './articles/alone-together/alone-together.component';
import { VanityComponent } from './articles/vanity/vanity.component';
import { FallingInLoveComponent } from './articles/falling-in-love/falling-in-love.component';
import { ArmsRaceComponent } from './articles/arms-race/arms-race.component';
import { ReworkComponent } from './articles/rework/rework.component';
import { AiUsageComponent } from './articles/ai-usage/ai-usage.component';
import { MondaysComponent } from './articles/mondays/mondays.component';
import { UpdatedAuthoringProcessComponent } from './articles/updated-authoring-process/updated-authoring-process.component';
import { AutomationComponent } from './articles/automation/automation.component';
import { IntroducingFrankensteinComponent } from './articles/introducing-frankenstein/introducing-frankenstein.component';
import { ChatGptComponent } from './articles/chat-gpt/chat-gpt.component';
import { HowItWorksComponent } from './articles/how-it-works/how-it-works.component';
import { WebsiteInfrastructureComponent } from './articles/website-infrastructure/website-infrastructure.component';
import { MoreOnMotivationComponent } from './articles/more-on-motivation/more-on-motivation.component';
import { SchooledByChatgptComponent } from './articles/schooled-by-chatgpt/schooled-by-chatgpt.component';
import { BridgeComponent } from './articles/bridge/bridge.component';
import { SandboxComponent } from './sandbox/sandbox.component';
import { ArticlePresenterComponent } from './article-presenter/article-presenter.component';


/*--BEGIN ROUTES--*/
const routes: Routes = [
  {path: 'articles/the-old-mill-town', component:MillTownComponent},
  {path: 'articles/friends-and-enemies', component:FriendsComponent},
  {path: 'articles/no-shame', component:NoShameComponent},
  {path: 'articles/aloof', component:AloofComponent},
  {path: 'articles/noncompete-agreements', component:NoncompeteAgreementsComponent},
  {path: 'articles/alone-together', component:AloneTogetherComponent},
  {path: 'articles/vanity', component:VanityComponent},
  {path: 'articles/falling-in-love', component:FallingInLoveComponent},
  {path: 'articles/a-dangerous-race', component:ArmsRaceComponent},
  {path: 'articles/rework', component:ReworkComponent},
  {path: 'articles/ai-usage', component:AiUsageComponent},
  {path: 'articles/mondays', component:MondaysComponent},
  {path: 'articles/revised-authoring-process', component:UpdatedAuthoringProcessComponent},
  {path: 'articles/automation', component:AutomationComponent},
  {path: '', redirectTo: '/home', pathMatch: 'full' },
  {path: 'article-list', component: ArticleListComponent},
  {path: 'home', component:HomeComponent},
  {path: 'articles/chatgpt', component:ChatGptComponent},  
  {path: 'articles/how-it-works', component:HowItWorksComponent},  
  {path: 'articles/website-infrastructure', component:WebsiteInfrastructureComponent},  
  {path: 'articles/introducing-frankenstein', component:IntroducingFrankensteinComponent},   
  {path: 'articles/more-on-motivation', component:MoreOnMotivationComponent},
  {path: 'articles/schooled-by-chatgpt', component:SchooledByChatgptComponent},
  {path: 'articles/bridge', component:BridgeComponent},
  {path: 'sandbox', component: SandboxComponent},
  {path: 'another-bridge', component: ArticlePresenterComponent},
  {path: 'the-old-mill-town', component: ArticlePresenterComponent}

];
/*--END ROUTES--*/

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    initialNavigation: 'enabledBlocking'
})],
  exports: [RouterModule]

})
export class AppRoutingModule { }
