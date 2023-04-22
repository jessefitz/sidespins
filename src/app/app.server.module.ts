import { NgModule } from '@angular/core';
import { ServerModule } from '@angular/platform-server';
import { TransferState } from '@angular/platform-browser';


import { AppModule } from './app.module';
import { AppComponent } from './app.component';

@NgModule({
  imports: [
    AppModule,
    ServerModule,
  ],
  providers: [
    TransferState
  ],
  bootstrap: [AppComponent],
  
})
export class AppServerModule {}
