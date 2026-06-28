import {classes} from '#ui/utils';
import {Directive} from '@angular/core';

@Directive({
  selector: '[hlmSkeleton],hlm-skeleton',
  host: {
    'data-slot': 'skeleton',
  },
})
export class HlmSkeleton {
  constructor() {
    classes(() => 'bg-muted block rounded-md motion-safe:animate-pulse');
  }
}
