import {classes} from '#ui/utils';
import {Directive} from '@angular/core';

@Directive({
  selector: 'kbd[hlmKbdGroup]',
  host: {
    'data-slot': 'kbd-group',
  },
})
export class HlmKbdGroup {
  constructor() {
    classes(() => 'inline-flex items-center gap-1');
  }
}
