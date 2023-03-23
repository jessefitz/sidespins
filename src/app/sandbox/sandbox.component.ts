import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-sandbox',
  templateUrl: './sandbox.component.html',
  styleUrls: ['./sandbox.component.css']
})
export class SandboxComponent {

  message = '';
  
  constructor(private http: HttpClient) {
    this.http.get('/api/HelloWorldFromNode')
      .subscribe((resp: any) => {
        console.log(resp); // log the resp object to the console
        this.message = resp.message;
      });
  }
}
