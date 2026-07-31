import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SETTINGS_LOOKUP_TABS } from './settings-tabs';

@Component({
  selector: 'app-settings',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings {
  private readonly auth = inject(AuthService);
  protected readonly isManager = this.auth.isManager;
  protected readonly lookupTabs = SETTINGS_LOOKUP_TABS;
}
