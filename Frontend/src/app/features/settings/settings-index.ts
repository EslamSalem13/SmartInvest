import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SETTINGS_LOOKUP_TABS } from './settings-tabs';

interface SettingsCard {
  slug: string;
  label: string;
  description: string;
  managerOnly?: boolean;
}

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
  protected readonly isManager = this.auth.isManager;

  protected readonly cards: SettingsCard[] = [
    ...SETTINGS_LOOKUP_TABS.map((t) => ({
      slug: t.slug,
      label: t.label,
      description: LOOKUP_DESCRIPTIONS[t.key] ?? '',
    })),
    { slug: 'financial-years', label: 'السنوات المالية', description: 'تعديل بيانات وموازنات السنوات المالية' },
    { slug: 'contractors', label: 'المقاولون', description: 'بيانات المقاولين المسندة إليهم المشروعات' },
    { slug: 'agencies', label: 'الجهات التنفيذية', description: 'الجهات المسؤولة عن تنفيذ المشروعات' },
    { slug: 'measurements', label: 'القياسات', description: 'تعريفات القياسات المخصصة للمشروعات الفرعية' },
    { slug: 'users', label: 'إدارة المستخدمين', description: 'حسابات المستخدمين وصلاحياتهم', managerOnly: true },
  ];
}
