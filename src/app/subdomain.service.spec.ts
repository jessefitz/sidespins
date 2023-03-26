import { TestBed } from '@angular/core/testing';

import { SubdomainService } from './subdomain.service';

describe('SubdomainService', () => {
  let service: SubdomainService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SubdomainService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
