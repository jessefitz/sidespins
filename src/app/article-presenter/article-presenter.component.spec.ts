import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ArticlePresenterComponent } from './article-presenter.component';

describe('ArticlePresenterComponent', () => {
  let component: ArticlePresenterComponent;
  let fixture: ComponentFixture<ArticlePresenterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ArticlePresenterComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ArticlePresenterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
