import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RolesService } from '../../core/services/roles.service';
import { AppRole, PermissionGroup, RoleDetail } from '../../core/models/permission.models';

@Component({
  selector: 'app-roles',
  imports: [FormsModule],
  templateUrl: './roles.html',
  styleUrl: './roles.css',
})
export class RolesPage {
  private readonly rolesService = inject(RolesService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly roles = signal<AppRole[]>([]);
  protected readonly catalog = signal<PermissionGroup[]>([]);

  protected readonly total = computed(() => this.roles().length);
  protected readonly customCount = computed(() => this.roles().filter((r) => !r.isSystem).length);
  protected readonly assignedUsers = computed(() =>
    this.roles().reduce((sum, r) => sum + r.userCount, 0),
  );

  // ===== نموذج الإنشاء/التعديل =====
  protected readonly showForm = signal(false);
  protected readonly editing = signal<RoleDetail | null>(null);
  protected readonly fDisplayName = signal('');
  protected readonly fPermissions = signal<Set<string>>(new Set());
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.rolesService.getPermissionCatalog().subscribe({
      next: (groups) => this.catalog.set(groups),
      error: () => this.catalog.set([]),
    });

    this.rolesService.getRoles().subscribe({
      next: (data) => {
        this.roles.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل الأدوار. تأكد من تسجيل الدخول كسوبر أدمن.');
        this.loading.set(false);
      },
    });
  }

  // ===== شجرة الصلاحيات =====

  /** المفتاح المنتهي بـ .view هو مفتاح الصفحة نفسها. */
  protected pageKey(group: PermissionGroup): string | null {
    return group.items.find((i) => i.key.endsWith('.view'))?.key ?? null;
  }

  protected sectionItems(group: PermissionGroup) {
    return group.items.filter((i) => !i.key.endsWith('.view'));
  }

  protected isChecked(key: string): boolean {
    return this.fPermissions().has(key);
  }

  /** الصفحة مفعّلة = مفتاح .view مختار، أو المجموعة ليس بها .view وأي عنصر مختار. */
  protected isPageEnabled(group: PermissionGroup): boolean {
    const page = this.pageKey(group);
    return page ? this.isChecked(page) : group.items.some((i) => this.isChecked(i.key));
  }

  protected togglePermission(key: string, checked: boolean): void {
    const next = new Set(this.fPermissions());
    if (checked) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this.fPermissions.set(next);
  }

  /** إيقاف الصفحة يزيل كل أقسامها أيضًا. */
  protected togglePage(group: PermissionGroup, checked: boolean): void {
    const next = new Set(this.fPermissions());
    const page = this.pageKey(group);

    if (checked) {
      if (page) next.add(page);
    } else {
      for (const item of group.items) {
        next.delete(item.key);
      }
    }

    this.fPermissions.set(next);
  }

  protected groupSelectedCount(group: PermissionGroup): number {
    return group.items.filter((i) => this.isChecked(i.key)).length;
  }

  // ===== إجراءات =====

  protected openCreate(): void {
    this.editing.set(null);
    this.fDisplayName.set('');
    this.fPermissions.set(new Set());
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEdit(role: AppRole): void {
    this.formError.set(null);
    this.rolesService.getRole(role.id).subscribe({
      next: (detail) => {
        this.editing.set(detail);
        this.fDisplayName.set(detail.displayName);
        this.fPermissions.set(new Set(detail.permissions));
        this.showForm.set(true);
      },
      error: (err) => alert(err?.error?.message ?? 'تعذّر تحميل بيانات الدور'),
    });
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    const displayName = this.fDisplayName().trim();
    if (!displayName) {
      this.formError.set('برجاء إدخال اسم الدور');
      return;
    }

    const permissions = [...this.fPermissions()];
    if (permissions.length === 0) {
      this.formError.set('برجاء اختيار صلاحية واحدة على الأقل');
      return;
    }

    this.saving.set(true);
    const dto = { displayName, permissions };
    const current = this.editing();
    const request = current
      ? this.rolesService.updateRole(current.id, dto)
      : this.rolesService.createRole(dto);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ الدور');
      },
    });
  }

  protected remove(role: AppRole): void {
    if (!confirm(`تأكيد حذف الدور «${role.displayName}»؟`)) {
      return;
    }

    this.rolesService.deleteRole(role.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف الدور'),
    });
  }
}
