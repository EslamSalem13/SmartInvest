export type TabKey =
  | 'mainProgram'
  | 'subProgram'
  | 'governorate'
  | 'markaz'
  | 'village'
  | 'priority'
  | 'status'
  | 'componentType'
  | 'projectLevel'
  | 'accountingUnit'
  | 'contractType'
  | 'unit';

export interface TabDef {
  key: TabKey;
  slug: string;
  label: string;
  addLabel: string;
  hasParent: boolean;
  parentLabel: string;
}

export const SETTINGS_LOOKUP_TABS: TabDef[] = [
  { key: 'mainProgram', slug: 'main-programs', label: 'البرامج الرئيسية', addLabel: 'إضافة برنامج رئيسي', hasParent: false, parentLabel: '' },
  { key: 'subProgram', slug: 'sub-programs', label: 'البرامج الفرعية', addLabel: 'إضافة برنامج فرعي', hasParent: true, parentLabel: 'البرنامج الرئيسي' },
  { key: 'governorate', slug: 'governorates', label: 'المحافظات', addLabel: 'إضافة محافظة', hasParent: false, parentLabel: '' },
  { key: 'markaz', slug: 'markaz', label: 'المراكز', addLabel: 'إضافة مركز', hasParent: true, parentLabel: 'المحافظة' },
  { key: 'village', slug: 'villages', label: 'القرى', addLabel: 'إضافة قرية', hasParent: true, parentLabel: 'المركز' },
  { key: 'priority', slug: 'priorities', label: 'الأولويات', addLabel: 'إضافة أولوية', hasParent: false, parentLabel: '' },
  { key: 'status', slug: 'statuses', label: 'حالات المشروع', addLabel: 'إضافة حالة', hasParent: false, parentLabel: '' },
  { key: 'componentType', slug: 'component-types', label: 'المكوّن العيني', addLabel: 'إضافة مكوّن عيني', hasParent: false, parentLabel: '' },
  { key: 'projectLevel', slug: 'project-levels', label: 'مستوى المشروع', addLabel: 'إضافة مستوى', hasParent: false, parentLabel: '' },
  { key: 'accountingUnit', slug: 'accounting-units', label: 'الوحدة الحسابية', addLabel: 'إضافة وحدة حسابية', hasParent: false, parentLabel: '' },
  { key: 'contractType', slug: 'contract-types', label: 'أنواع العقود', addLabel: 'إضافة نوع عقد', hasParent: false, parentLabel: '' },
  { key: 'unit', slug: 'units', label: 'وحدات القياس', addLabel: 'إضافة وحدة قياس', hasParent: false, parentLabel: '' },
];
