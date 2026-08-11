export interface DashboardYear {
  financialYearId: number;
  financialYearName: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
}

export interface DashboardProjectMetrics {
  totalSubProjects: number;
  approvedCount: number;
  proposedCount: number;
  stalledCount: number;
  approvalRate: number;
  completedCount: number;
  inProgressCount: number;
  averagePhysicalProgress: number;
}

export interface DashboardFinancialMetrics {
  totalFunding: number;
  bankFunding: number;
  selfFunding: number;
  totalBankAvailabilities: number;
  remainingAvailableToBank: number;
  availabilityRateOfBankFunding: number;
  bankSpent: number;
  selfSpent: number;
  totalSpent: number;
  spentRateOfTotalFunding: number;
}

export interface DashboardNamedValue {
  name: string;
  value: number;
}

export interface DashboardProgramFunding {
  programName: string;
  projectCount: number;
  bankFunding: number;
  selfFunding: number;
  totalFunding: number;
}

export interface DashboardAvailabilityPoint {
  receivedDate: string;
  amount: number;
  cumulativeAmount: number;
}

export interface DashboardCharts {
  fundingDistribution: DashboardNamedValue[];
  statusDistribution: DashboardNamedValue[];
  priorityDistribution: DashboardNamedValue[];
  markazDistribution: DashboardNamedValue[];
  programFunding: DashboardProgramFunding[];
  progressDistribution: DashboardNamedValue[];
  availabilityTimeline: DashboardAvailabilityPoint[];
}

export interface DashboardAvailabilityBrief {
  id: number;
  amount: number;
  receivedDate: string;
}

export interface DashboardProjectBrief {
  subProjectId: number;
  subProjectName: string;
  subProjectCode: string | null;
  mainProjectName: string;
  totalCost: number;
  isApproved: boolean;
}

export interface DashboardStageBrief {
  executionStageId: number;
  subProjectId: number;
  subProjectName: string;
  stageName: string;
  deadline: string | null;
}

export interface DashboardDetails {
  recentAvailabilities: DashboardAvailabilityBrief[];
  recentProjects: DashboardProjectBrief[];
  topFundedProjects: DashboardProjectBrief[];
  overdueStages: DashboardStageBrief[];
  stalledProjects: DashboardProjectBrief[];
  pendingApprovalProjects: DashboardProjectBrief[];
}

export interface DashboardOverview {
  year: DashboardYear;
  projectMetrics: DashboardProjectMetrics;
  financialMetrics: DashboardFinancialMetrics;
  charts: DashboardCharts;
  details: DashboardDetails;
}
