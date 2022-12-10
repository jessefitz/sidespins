import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ArticleListComponentComponent } from './article-list-component.component';

describe('ArticleListComponentComponent', () => {
  let component: ArticleListComponentComponent;
  let fixture: ComponentFixture<ArticleListComponentComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ArticleListComponentComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ArticleListComponentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
