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
}

export interface ExecutionStage {
  id: number;
  subProjectId: number;
  name: string;
  deadline: string;
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
