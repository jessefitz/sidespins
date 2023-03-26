import { Component } from '@angular/core';
import { OnInit } from '@angular/core';
import { SubdomainService } from './subdomain.service';
import { CookieService } from 'ngx-cookie-service';

// import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
  title = 'jessefitz_app';
  isImpersonating = false;
  message = '';
  personaValue = '';

  
  // constructor(private http: HttpClient) {
  //   this.http.get('/api/HelloWorldFromNode')
  //     .subscribe((resp: any) => {
  //       console.log(resp); // log the resp object to the console
  //       this.message = resp.message;
  //     });
  // }
  constructor(
    private subdomainService: SubdomainService,
    private cookieService: CookieService) {
   
  }
  ngOnInit() {
    //CHECK THE SUBDOMAIN... IT DRIVES BEHAVIOR.  subdomain is configured at the app service level using cname record in the jessefitz.me DNS zone.
    this.isImpersonating = this.subdomainService.getSubdomain() == 'not';
    const savedValue = this.cookieService.get('personaValue');
    if (savedValue) {
      this.personaValue = savedValue;
    }
  }

  saveInputValueToCookie(): void {
    this.cookieService.set('personaValue', this.personaValue);
  }


}
