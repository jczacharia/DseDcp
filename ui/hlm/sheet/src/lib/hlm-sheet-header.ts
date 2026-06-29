import {classes} from '#hlm/utils';
import {Directive} from '@angular/core';

@Directive({
  selector: '[hlmSheetHeader],hlm-sheet-header',
  host: {'data-slot': 'sheet-header'},
})
export class HlmSheetHeader {
  constructor() {
    classes(() => 'flex flex-col gap-0.5 p-4');
  }
}
