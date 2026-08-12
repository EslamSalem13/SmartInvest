export const REPORT_KEYS = [
  'project-register',
  'funding-vs-spending',
  'bank-availability-ledger',
  'plan-approval-status',
  'procurement-pipeline',
  'contracts-contractors',
  'execution-delays',
  'geographic-distribution',
  'program-agency-performance',
  'measurements-outcomes',
] as const;

export type ReportKey = (typeof REPORT_KEYS)[number];

export interface ReportCatalogItem {
  key: string;
  title: string;
  description: string;
  includedFields: string[];
}

export interface AiReportRequest {
  prompt: string;
  financialYearId?: number | null;
}
