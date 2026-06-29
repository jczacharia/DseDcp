import {classes} from '#hlm/utils';
import {Directive} from '@angular/core';

@Directive({
  selector: '[hlmSidebarHeader],hlm-sidebar-header',
  host: {
    'data-slot': 'sidebar-header',
    'data-sidebar': 'header',
  },
})
export class HlmSidebarHeader {
  constructor() {
    classes(() => 'flex flex-col gap-2 p-2');
  }
}
