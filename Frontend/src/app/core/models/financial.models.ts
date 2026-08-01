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
  presentationMemos: PresentationMemoBrief[];
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
}

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
}

export interface PresentationMemoDetail extends PresentationMemo {
  versions: ProcurementVersion[];
}

export interface CreatePresentationMemo {
  title: string;
  subProjectIds: number[];
}

export type UpdatePresentationMemo = CreatePresentationMemo;
