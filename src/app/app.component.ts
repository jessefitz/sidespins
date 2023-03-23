import { Component } from '@angular/core';
// import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'jessefitz_app';
  message = '';
  
  // constructor(private http: HttpClient) {
  //   this.http.get('/api/HelloWorldFromNode')
  //     .subscribe((resp: any) => {
  //       console.log(resp); // log the resp object to the console
  //       this.message = resp.message;
  //     });
  // }
}
