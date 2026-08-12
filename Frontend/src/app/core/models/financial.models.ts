// ===== الإدارة المالية — مراحل الطرح =====

export interface ProcurementFile {
  key: string;
  label: string;
  fileName: string;
  fileExtension: string;
  fileSize: number;
}

export interface ProcurementVersion {
  id: number;
  versionNumber: number;
  notes: string | null;
  createdAt: string;
  files: ProcurementFile[];
  /** إصدارات مذكرة العرض فقط — تاريخ رفع قرار لجنة الشؤون القانونية */
  legalAffairsDecisionUploadedAt?: string | null;
}

/** بيانات مرحلة الترسية بخلاف الملفات. */
export interface ContractAwardDetails {
  projectNature: string;
  /** «مقاولات» فقط — «توريدات» لا يُصرف لها مقدَّم */
  requiresAdvancePayment: boolean;

  totalCost: number;
  bankFunding: number;
  selfFunding: number;

  advancePaymentDone: boolean;
  advancePaymentPercentage: number | null;
  advancePaymentSelfAmount: number | null;
  advancePaymentBankAmount: number | null;

  executionDurationMonths: number | null;
  executionDurationDays: number | null;
  /** 1 = مُسلَّمة وقت الترسية، 2 = لم تُسلَّم بعد */
  siteHandoverMode: number | null;
  siteHandoverDate: string | null;
  siteHandoverProofFileName: string | null;
  contractualDeliveryDate: string | null;

  penaltyAmount: number | null;

  contractorId: number | null;
  contractorName: string | null;
  contractTypeId: number | null;
  contractNumber: string | null;
  contractValue: number | null;
}

export interface SetContractAwardDetails {
  advancePaymentDone: boolean;
  advancePaymentPercentage: number | null;
  advancePaymentSelfAmount: number | null;
  advancePaymentBankAmount: number | null;
  executionDurationMonths: number | null;
  executionDurationDays: number | null;
  siteHandoverMode: number | null;
  penaltyAmount: number | null;
  contractorId: number | null;
  contractTypeId: number | null;
  contractNumber: string | null;
  contractValue: number | null;
}

export interface ProcurementFileSlot {
  key: string;
  label: string;
  required: boolean;
}

export interface ProcurementStage {
  stage: string;
  stageLabel: string;
  order: number;
  documentId: number | null;
  currentVersionNumber: number;
  isCompleted: boolean;
  lastUpdatedAt: string | null;
  fileSlots: ProcurementFileSlot[];
  isLocked: boolean;
  /** تأكيد صرف الدفعة المقدمة 25% — قيمة غير فارغة فقط لمرحلة "العقد والترسية" */
  advancePaymentDone: boolean | null;
  contractAward?: ContractAwardDetails | null;
}

export interface ProcurementStageDetail extends ProcurementStage {
  versions: ProcurementVersion[];
}

export interface PresentationMemoBrief {
  id: number;
  title: string;
  currentVersionNumber: number;
  isCompleted: boolean;
}

export interface ProcurementOverview {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  /** المذكرة الفعّالة فقط — الأحدث، وليست كل المذكرات المرتبطة تاريخيًا */
  activePresentationMemo: PresentationMemoBrief | null;
  stages: ProcurementStage[];
}

export interface ProcurementSubProjectListItem {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  mainProjectName: string;
  completedStages: number;
  totalStages: number;
  hasPresentationMemo: boolean;
  activeMemoId: number | null;
  activeMemoTitle: string | null;
  contractingMethod: number | null;
  contractingMethodLabel: string | null;
}

/** أسماء مراحل الطرح الست بالترتيب — الفهرس = عدد المراحل المكتملة قبلها */
export const PROCUREMENT_STAGE_NAMES: readonly string[] = [
  'كراسة الشروط',
  'الإعلان',
  'فتح المظاريف',
  'التقييم الفني',
  'التقييم المالي',
  'العقد والترسية',
];

// ===== مذكرات العرض =====

export interface MemoSubProject {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
}

export interface PresentationMemo {
  id: number;
  title: string;
  currentVersionNumber: number;
  isCompleted: boolean;
  createdAt: string;
  subProjects: MemoSubProject[];
  /** رقم من ContractingMethod — فارغ للمذكرات المُنشأة قبل إضافة الحقل */
  contractingMethod: number | null;
  contractingMethodLabel: string | null;
}

export interface PresentationMemoDetail extends PresentationMemo {
  versions: ProcurementVersion[];
}

export interface CreatePresentationMemo {
  title: string;
  subProjectIds: number[];
  contractingMethod: number | null;
}

export type UpdatePresentationMemo = CreatePresentationMemo;

/**
 * طرق التعاقد — مرتّبة من الأقل تنافسية إلى الأكثر.
 * القيم تطابق enum ContractingMethod في الـ Backend.
 */
export const CONTRACTING_METHODS: ReadonlyArray<{ value: number; label: string }> = [
  { value: 1, label: 'إسناد مباشر' },
  { value: 2, label: 'الاتفاق المباشر' },
  { value: 3, label: 'ممارسة محدودة' },
  { value: 4, label: 'الممارسة العامة' },
  { value: 5, label: 'مناقصة خاصة' },
  { value: 6, label: 'مناقصة عامة' },
  { value: 7, label: 'المناقصة ذات المرحلتين' },
];
