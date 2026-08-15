import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SETTINGS_LOOKUP_TABS } from './settings-tabs';

interface SettingsCard {
  slug: string;
  label: string;
  description: string;
  section: SettingsSectionKey;
  managerOnly?: boolean;
  superAdminOnly?: boolean;
}

type SettingsSectionKey = 'structure' | 'projects' | 'operations' | 'system';

interface SettingsSection {
  key: SettingsSectionKey;
  title: string;
  description: string;
}

const LOOKUP_SECTIONS: Record<string, SettingsSectionKey> = {
  mainProgram: 'structure',
  subProgram: 'structure',
  governorate: 'structure',
  markaz: 'structure',
  village: 'structure',
  priority: 'projects',
  status: 'projects',
  componentType: 'projects',
  projectLevel: 'projects',
  accountingUnit: 'projects',
  contractType: 'projects',
  unit: 'projects',
};

const LOOKUP_DESCRIPTIONS: Record<string, string> = {
  mainProgram: 'البرامج الرئيسية للخطة الاستثمارية',
  subProgram: 'البرامج الفرعية التابعة لكل برنامج رئيسي',
  governorate: 'المحافظات المستخدمة في تصنيف المشروعات',
  markaz: 'المراكز التابعة لكل محافظة',
  village: 'القرى التابعة لكل مركز',
  priority: 'أولويات تنفيذ المشروعات',
  status: 'حالات تنفيذ المشروع',
  componentType: 'أنواع المكوّن العيني للمشروعات',
  projectLevel: 'مستويات المشروع',
  accountingUnit: 'الوحدات الحسابية للمشروعات',
  contractType: 'أنواع العقود مع المقاولين',
  unit: 'وحدات القياس المستخدمة في القياسات المخصصة',
};

@Component({
  selector: 'app-settings-index',
  imports: [RouterLink],
  templateUrl: './settings-index.html',
  styleUrl: './settings-index.css',
})
export class SettingsIndex {
  private readonly auth = inject(AuthService);
  protected readonly canManageUsers = this.auth.canManageUsers;
  protected readonly isSuperAdmin = this.auth.isSuperAdmin;
  protected readonly query = signal('');

  protected readonly sections: SettingsSection[] = [
    { key: 'structure', title: 'هيكل الخطة والمواقع', description: 'البرامج والتقسيمات الجغرافية المستخدمة في بناء الخطة' },
    { key: 'projects', title: 'تعريفات المشروعات', description: 'القيم المرجعية التي تصف المشروع وحالته وقياساته' },
    { key: 'operations', title: 'التشغيل والجهات', description: 'السنوات المالية والأطراف المسؤولة عن تنفيذ المشروعات' },
    { key: 'system', title: 'النظام والمستخدمون', description: 'إدارة الحسابات ومتابعة عمليات النظام الحساسة' },
  ];

  protected readonly cards: SettingsCard[] = [
    ...SETTINGS_LOOKUP_TABS.map((t) => ({
      slug: t.slug,
      label: t.label,
      description: LOOKUP_DESCRIPTIONS[t.key] ?? '',
      section: LOOKUP_SECTIONS[t.key] ?? 'projects',
    })),
    { slug: 'financial-years', label: 'السنوات المالية', description: 'تعديل بيانات وموازنات السنوات المالية', section: 'operations' },
    { slug: 'contractors', label: 'المقاولون', description: 'بيانات المقاولين المسندة إليهم المشروعات', section: 'operations' },
    { slug: 'agencies', label: 'الجهات التنفيذية', description: 'الجهات المسؤولة عن تنفيذ المشروعات', section: 'operations' },
    { slug: 'measurements', label: 'القياسات', description: 'تعريفات القياسات المخصصة للمشروعات الفرعية', section: 'projects' },
    { slug: 'users', label: 'إدارة المستخدمين', description: 'حسابات المستخدمين وصلاحياتهم', section: 'system', managerOnly: true },
    { slug: 'plan-approval-notifications', label: 'إشعارات اعتماد الخطط', description: 'متابعة إرسال البريد للمستلمين وإعادة محاولة الرسائل الفاشلة', section: 'system', superAdminOnly: true },
  ];

  protected updateSearch(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value.trim().toLocaleLowerCase('ar'));
  }

  protected visibleCards(section: SettingsSectionKey): SettingsCard[] {
    const query = this.query();
    return this.cards.filter((card) =>
      card.section === section &&
      (!card.managerOnly || this.canManageUsers()) &&
      (!card.superAdminOnly || this.isSuperAdmin()) &&
      (!query || `${card.label} ${card.description}`.toLocaleLowerCase('ar').includes(query)),
    );
  }
}
