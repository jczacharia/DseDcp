import {classes} from '#hlm/utils';
import {Directive} from '@angular/core';
import {BrnSheetDescription} from '@spartan-ng/brain/sheet';

@Directive({
  selector: '[hlmSheetDescription]',
  hostDirectives: [BrnSheetDescription],
  host: {'data-slot': 'sheet-description'},
})
export class HlmSheetDescription {
  constructor() {
    classes(() => 'text-muted-foreground text-sm');
  }
}
