import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface SettingsLookupItem {
  id: number;
  name: string;
  parentId?: number;
  parentName?: string;
}

export interface SettingsLookupParentOption {
  id: number;
  name: string;
}

export interface SettingsLookupSaveEvent {
  id: number | null;
  name: string;
  parentId: number | null;
}

@Component({
  selector: 'app-settings-lookup-table',
  imports: [FormsModule],
  templateUrl: './settings-lookup-table.html',
  styleUrl: './settings-lookup-table.css',
})
export class SettingsLookupTable {
  readonly title = input.required<string>();
  readonly addLabel = input.required<string>();
  readonly nameLabel = input('الاسم');
  readonly hasParent = input(false);
  readonly parentLabel = input('');
  readonly parentOptions = input<SettingsLookupParentOption[]>([]);
  readonly items = input.required<SettingsLookupItem[]>();
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly isManager = input(false);

  readonly save = output<SettingsLookupSaveEvent>();
  readonly remove = output<number>();

  protected readonly search = computed(() => this._search());
  private readonly _search = signal('');

  protected readonly filtered = computed(() => {
    const term = this._search().trim().toLowerCase();
    if (!term) return this.items();
    return this.items().filter((i) => i.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly formName = signal('');
  protected readonly formParentId = signal<number | null>(null);

  protected setSearch(value: string): void {
    this._search.set(value);
  }

  protected openAdd(): void {
    this.editingId.set(null);
    this.formName.set('');
    this.formParentId.set(null);
    this.showForm.set(true);
  }

  protected openEdit(item: SettingsLookupItem): void {
    this.editingId.set(item.id);
    this.formName.set(item.name);
    this.formParentId.set(item.parentId ?? null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    const name = this.formName().trim();
    if (!name) return;
    if (this.hasParent() && this.formParentId() == null) return;

    this.save.emit({
      id: this.editingId(),
      name,
      parentId: this.formParentId(),
    });
    this.showForm.set(false);
  }

  protected onDelete(item: SettingsLookupItem): void {
    if (!confirm(`تأكيد حذف «${item.name}»؟`)) return;
    this.remove.emit(item.id);
  }
}
