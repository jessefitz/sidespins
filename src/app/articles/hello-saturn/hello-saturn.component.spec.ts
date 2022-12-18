import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HelloSaturnComponent } from './hello-saturn.component';

describe('HelloSaturnComponent', () => {
  let component: HelloSaturnComponent;
  let fixture: ComponentFixture<HelloSaturnComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ HelloSaturnComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(HelloSaturnComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
