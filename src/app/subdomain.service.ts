import { Injectable } from '@angular/core';
import { isPlatformBrowser, isPlatformServer } from '@angular/common';
import { Inject, PLATFORM_ID } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubdomainService {
  private subdomain: string;

  constructor( @Inject(PLATFORM_ID) private platformId: Object) {
    this.subdomain = "";
    if (isPlatformBrowser(this.platformId)) {
      this.subdomain = this.extractSubdomain(window.location.hostname);
    }
  }

  private extractSubdomain(hostname: string): string {
    const parts = hostname.split('.');
    if (parts.length >= 2) {
      return parts[0];
    }
    return '';
  }

  public getSubdomain(): string {
    return this.subdomain;
  }

  // Add any other helper methods related to subdomain-specific behaviors
}
