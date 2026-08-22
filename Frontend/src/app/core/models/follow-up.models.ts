export interface FollowUpListItem {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  mainProjectName: string;
  contractorName: string | null;
  isStalled: boolean;
  financialProgressPercent: number;
  physicalProgressPercent: number;
  nextDeadline: string | null;
  stageCount: number;
  /** المتبقي من التمويل الذاتي (ج.م كامل) = التمويل الذاتي الإجمالي − منصرف مراحل التنفيذ − الدفعة المقدمة من هذا المصدر. */
  remainingSelfFunding: number;
  /** المتبقي من التمويل البنكي — نفس منطق remainingSelfFunding لكن للمصدر البنكي. */
  remainingBankFunding: number;
  completionEligibility: ProjectCompletionEligibility;
}

export interface ProjectCompletionEligibility {
  isProjectCompleted: boolean;
  canCompleteProject: boolean;
  contractValue: number | null;
  selfFundingSpent: number;
  bankFundingSpent: number;
  totalSpent: number;
  /** الدفعة المقدمة المصروفة — جزء من إجمالي المنصرف، تُعرض منفصلة للتوضيح */
  advancePaymentTotal: number;
  overrunPercentage: number;
  minimumRequired: number | null;
  maximumAllowed: number | null;
  physicalProgressTotal: number;
  allStagesCompleted: boolean;
  hasExecutionStages: boolean;
  blockers: string[];
}

export interface ExecutionStage {
  id: number;
  subProjectId: number;
  financialYearId: number | null;
  name: string;
  /** الموعد الابتدائي — null للمراحل المسجَّلة قبل إضافة الحقل */
  startDate: string | null;
  /** null فقط لمرحلة التسليم النهائي قبل تسليم الأرضية */
  deadline: string | null;
  isFinalDelivery: boolean;
  /** مرحلة الدفعة المقدمة التلقائية — بياناتها من العقد والترسية، مقفولة هنا */
  isAdvancePayment: boolean;
  exceedsContractualDeadline: boolean;
  selfFundingSpent: number;
  bankFundingSpent: number;
  hasSelfFundingProof: boolean;
  hasBankFundingProof: boolean;
  selfFundingProofFileName: string | null;
  bankFundingProofFileName: string | null;
  physicalProgressPercent: number;
  hasPhysicalProgressProof: boolean;
  physicalProgressProofFileName: string | null;
  notes: string | null;
  penaltyAmount: number | null;
  penaltyPaid: boolean;
  isCompleted: boolean;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateExecutionStagePayload {
  name: string;
  startDate: string;
  deadline: string;
  selfFundingSpent: number;
  bankFundingSpent: number;
  physicalProgressPercent: number;
  notes: string;
  selfFundingProof: File | null;
  bankFundingProof: File | null;
  physicalProgressProof: File | null;
}

export interface FollowUpFilters {
  financialYearId?: number | null;
  mainProgramId?: number | null;
  subProgramId?: number | null;
  markazId?: number | null;
  priorityId?: number | null;
  searchTerm?: string | null;
}

/** خط زمني حياة المشروع الكامل (كل السنوات المالية) — لمخطط "تطور التنفيذ" بلوحة التحكم. */
export interface ExecutionTimeline {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  /** false = لا توجد ترسية مكتملة بقيمة عقد صحيحة بعد — points فارغة والسقوف null. */
  hasContractValue: boolean;
  contractValue: number | null;
  totalCost: number;
  overrunPercentage: number;
  /** = 100 دائمًا عند hasContractValue. */
  contractValueCeilingPercent: number | null;
  maxAllowedCeilingPercent: number | null;
  points: ExecutionTimelinePoint[];
}

export interface ExecutionTimelinePoint {
  date: string;
  /** اسم المرحلة، أو "الدفعة المقدمة"، أو "اليوم". */
  label: string;
  cumulativeProgressPercent: number;
  cumulativeSpendPercent: number;
}
