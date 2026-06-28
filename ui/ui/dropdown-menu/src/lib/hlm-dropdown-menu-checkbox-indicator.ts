import {classes} from '#ui/utils';
import {ChangeDetectionStrategy, Component} from '@angular/core';
import {NgIcon, provideIcons} from '@ng-icons/core';
import {lucideCheck} from '@ng-icons/lucide';

@Component({
  selector: 'hlm-dropdown-menu-checkbox-indicator',
  imports: [NgIcon],
  providers: [provideIcons({lucideCheck})],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {'data-slot': 'dropdown-menu-checkbox-item-indicator'},
  template: ` <ng-icon name="lucideCheck" /> `,
})
export class HlmDropdownMenuCheckboxIndicator {
  constructor() {
    classes(
      () =>
        'pointer-events-none absolute end-2 flex items-center justify-center opacity-0 group-data-checked/dropdown-menu-checkbox:opacity-100 [&_ng-icon]:text-[length:--spacing(4)]',
    );
  }
}
