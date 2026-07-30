export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Lookup {
  id: number;
  name: string;
}

export interface SubProgramLookup extends Lookup {
  mainProgramId: number;
}

export interface MarkazLookup extends Lookup {
  governorateId: number;
}

export interface VillageLookup extends Lookup {
  markazId: number;
}

export interface MainProjectListItem {
  id: number;
  code: string | null;
  isApproved: boolean;
  name: string;
  executingAgency: string;
  subProgramId: number;
  subProgramName: string;
  mainProgramName: string;
  subProjectsCount: number;
  totalBankFunding: number;
  totalSelfFunding: number;
}

export interface SubProjectListItem {
  id: number;
  code: string | null;
  name: string;
  mainProjectId: number;
  mainProjectCode: string;
  mainProjectName: string;
  projectLevelId: number;
  projectLevelName: string;
  componentTypeId: number;
  componentTypeName: string;
  markazId: number;
  markazName: string;
  priorityId: number;
  priorityName: string;
  statusId: number;
  statusName: string;
  isApproved: boolean;
  approvalCancellationReason: string | null;
  approvedAt: string | null;
  approvalCancelledAt: string | null;
  bankFunding: number;
  selfFunding: number;
  totalCost: number;
  executiveAgencyId: number | null;
  executiveAgencyName: string | null;
  contractorName: string | null;
}

export interface SubProjectDetail {
  id: number;
  code: string | null;
  name: string;
  mainProjectId: number;
  mainProjectName: string;
  projectLevelId: number;
  projectLevelName: string;
  componentTypeId: number;
  componentTypeName: string;
  accountingUnitId: number;
  accountingUnitName: string;
  projectNature: string;
  description: string | null;
  goal: string | null;
  socialImpact: string | null;
  economicImpact: string | null;
  environmentalImpact: string | null;
  greenInvestmentLink: string | null;
  markazId: number;
  markazName: string;
  governorateId: number;
  governorateName: string;
  latitude: number | null;
  longitude: number | null;
  priorityId: number;
  priorityName: string;
  statusId: number;
  statusName: string;
  isApproved: boolean;
  approvalCancellationReason: string | null;
  approvedAt: string | null;
  approvalCancelledAt: string | null;
  bankFunding: number;
  selfFunding: number;
  totalCost: number;
}

export interface MainProjectDetail {
  id: number;
  code: string | null;
  isApproved: boolean;
  name: string;
  executingAgency: string;
  subProgramId: number;
  subProgramName: string;
  mainProgramName: string;
  subProjects: SubProjectListItem[];
}

export interface CreateMainProject {
  code: string | null;
  name: string;
  executingAgency: string;
  subProgramId: number;
}

export interface UpdateMainProject {
  code: string | null;
  name: string;
  executingAgency: string;
  subProgramId: number;
}

export interface MainProjectDetailBase {
  executingAgency: string;
}

/** قائمة جهات التنفيذ الثابتة */
export const EXECUTING_AGENCIES = [
  'الإدارة المالية',
  'الوحدة المحلية شبين',
  'الوحدة المحلية لمدينة قويسنا',
  'شركة الكهرباء',
  'شركة المنوفية لصيانة الآليات',
  'شركة مياه الشرب',
];

export interface CreateSubProject {
  mainProjectId: number;
  code?: string | null;
  name: string;
  projectLevelId: number;
  componentTypeId: number;
  accountingUnitId: number;
  projectNature: string;
  markazId: number;
  priorityId: number;
  statusId: number;
  bankFunding: number;
  selfFunding: number;
  latitude?: number | null;
  longitude?: number | null;
  description?: string | null;
  goal?: string | null;
  socialImpact?: string | null;
  economicImpact?: string | null;
  environmentalImpact?: string | null;
  greenInvestmentLink?: string | null;
}

export type UpdateSubProject = Omit<CreateSubProject, 'mainProjectId'>;

export interface SubProjectSearchParams {
  mainProjectId?: number;
  mainProgramId?: number;
  subProgramId?: number;
  markazId?: number;
  priorityId?: number;
  statusId?: number;
  financialYearId?: number;
  searchTerm?: string;
  page: number;
  pageSize: number;
}

export interface FinancialYear {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  budget: number | null;
}

export interface CreateFinancialYear {
  name: string;
  startDate: string;
  endDate: string;
  budget?: number | null;
}

export interface UpdateFinancialYear {
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  budget?: number | null;
}

export interface SubProjectFinancialYear {
  id: number;
  financialYearId: number;
  financialYearName: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
}

export interface Plan {
  planId: number;
  planName: string;
  startDate: string;
  endDate: string;
  planStatus: string;
  approvalDate: string | null;
  financialYearId: number;
  financialYearName: string;
  suggestionDate: string;
}

export interface PlanProjectItem {
  subProjectName: string;
  mainProjectId: number;
  projectLevel: string;
  componentType: string;
  accountingUnit: string;
  totalCost: number;
  projectNature: string;
  greenInvestmentLink?: string | null;
  projectDescription?: string | null;
  projectGoal?: string | null;
  socialImpact?: string | null;
  economicImpact?: string | null;
  environmentalImpact?: string | null;
  markazId: number;
  priorityId: number;
  executiveAgencyId?: number | null;
  latitude?: number | null;
  longitude?: number | null;
  statusId: number;
}

export interface PlanDetail extends Plan {
  projects: PlanProjectItem[] | null;
}

export interface ProjectInfo {
  subProjectId: number;
  subProjectName: string;
  projectLevel: string;
  totalCost: number;
  executiveAgencyName: string | null;
}

export interface CreatePlan {
  planName: string;
  startDate: string;
  endDate: string;
  planStatus: string;
  approvalDate?: string | null;
  financialYearId: number;
}

export type UpdatePlan = CreatePlan;

export interface CreatedPlan {
  planId: number;
  planName: string;
  planStatus: number;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  suggestionDate: string;
  approvalDate: string | null;
  financialYearId: number;
}

export interface ApprovePlan {
  approvalDate: string;
}

export interface AssignedSubProject {
  id: number;
  name: string;
  mainProjectName: string;
}

export interface Contractor {
  id: number;
  contractorName: string;
  companyType: string;
  nationalIdOrCommercialRegister: string;
  phoneNumber: string;
  email: string;
  address: string;
  category: string;
  isActive: boolean;
  assignedSubProjects: AssignedSubProject[];
}

export interface CreateContractor {
  contractorName: string;
  companyType: string;
  nationalIdOrCommercialRegister: string;
  phoneNumber: string;
  email: string;
  address: string;
  category: string;
}

export interface UpdateContractor extends CreateContractor {
  isActive: boolean;
}

export interface ExecutiveAgencyProfile {
  id: number;
  agencyName: string;
  phone: string;
  email: string;
  address: string;
  isActive: boolean;
  assignedSubProjects: AssignedSubProject[];
}

export interface CreateAgency {
  agencyName: string;
  phone: string;
  email: string;
  address: string;
}

export interface UpdateAgency extends CreateAgency {
  isActive: boolean;
}

export interface CreateNamedLookup {
  name: string;
}

export type UpdateNamedLookup = CreateNamedLookup;

export interface CreateSubProgram {
  name: string;
  mainProgramId: number;
}

export type UpdateSubProgram = CreateSubProgram;

export interface CreateMarkaz {
  name: string;
  governorateId: number;
}

export type UpdateMarkaz = CreateMarkaz;

export interface CreateVillage {
  name: string;
  markazId: number;
}

export type UpdateVillage = CreateVillage;

export interface Measurement {
  id: number;
  name: string;
  unit: string;
  subProgramIds: number[];
  subProgramNames: string[];
}

export interface CreateMeasurement {
  name: string;
  unit: string;
  subProgramIds: number[];
}

export type UpdateMeasurement = CreateMeasurement;

export interface SubProjectMeasurementValue {
  measurementId: number;
  measurementName: string;
  unit: string;
  value: number | null;
}

export interface SetMeasurementValue {
  measurementId: number;
  value: number | null;
}
