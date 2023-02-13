import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { MarkdownModule } from 'ngx-markdown';
import { HttpClientModule, HttpClient } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { ArticleListComponent } from './article-list/article-list.component';
import { HomeComponent } from './home/home.component';
// import { ArticleComponent } from './article/article.component';
// import { HelloWorldComponent } from './articles/hello-world/hello-world.component';
// import { HelloMarsComponent } from './articles/hello-mars/hello-mars.component';
// import { HelloSaturnComponent } from './articles/hello-saturn/hello-saturn.component';
import { HelloVenusComponent } from './articles/hello-venus/hello-venus.component';
import { IntroducingFrankensteinComponent } from './articles/introducing-frankenstein/introducing-frankenstein.component';
import { WebsiteInfrastructureComponent } from './articles/website-infrastructure/website-infrastructure.component';
import { HowItWorksComponent } from './articles/how-it-works/how-it-works.component';
import { ChatGptComponent } from './articles/chat-gpt/chat-gpt.component';
import { MoreOnMotivationComponent } from './articles/more-on-motivation/more-on-motivation.component';
import { SchooledByChatgptComponent } from './articles/schooled-by-chatgpt/schooled-by-chatgpt.component';
import { BridgeComponent } from './articles/bridge/bridge.component';
import { AutomationComponent } from './articles/automation/automation.component';
import { UpdatedAuthoringProcessComponent } from './articles/updated-authoring-process/updated-authoring-process.component';
import { MondaysComponent } from './articles/mondays/mondays.component';

@NgModule({
  declarations: [
    AppComponent,
    ArticleListComponent,
    HomeComponent,
    HelloVenusComponent,
    IntroducingFrankensteinComponent,
    WebsiteInfrastructureComponent,
    HowItWorksComponent,
    ChatGptComponent,
    MoreOnMotivationComponent,
    SchooledByChatgptComponent,
    BridgeComponent,
    AutomationComponent,
    UpdatedAuthoringProcessComponent,
    MondaysComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'serverApp' }),
    AppRoutingModule,
    HttpClientModule,
    MarkdownModule.forRoot({ loader: HttpClient }), 
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
