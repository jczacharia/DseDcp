import {classes} from '#hlm/utils';
import {Directive} from '@angular/core';

@Directive({
  selector: '[hlmFieldContent],hlm-field-content',
  host: {'data-slot': 'field-content'},
})
export class HlmFieldContent {
  constructor() {
    classes(() => 'group/field-content flex flex-1 flex-col gap-0.5 leading-snug');
  }
}
