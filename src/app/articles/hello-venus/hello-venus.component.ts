//ng generate component articles/hello-moon --module=app.module --inline-template --inline-style --skip-tests --route /articles/hello-moon      
import { Component } from '@angular/core';

@Component({
  selector: 'app-hello-venus',
  template: `<markdown src="assets/article-content/chatgpt.md"></markdown>`,
  styles: [
  ]
})
export class HelloVenusComponent {

}
