export enum PlanApprovalNotificationStatus {
  Pending = 0,
  Processing = 1,
  Sent = 2,
  PartiallyFailed = 3,
  Failed = 4,
  NoRecipients = 5,
}

export enum PlanApprovalRecipientStatus {
  Pending = 0,
  Sent = 1,
  Failed = 2,
}

export interface PlanApprovalNotificationListItem {
  id: number;
  planId: number;
  planName: string;
  financialYearName: string;
  status: PlanApprovalNotificationStatus;
  createdAtUtc: string;
  completedAtUtc: string | null;
  attemptCount: number;
  recipientCount: number;
  sentCount: number;
  failedCount: number;
  lastError: string | null;
}

export interface PlanApprovalNotificationRecipient {
  id: number;
  fullName: string;
  email: string;
  role: string;
  status: PlanApprovalRecipientStatus;
  attemptCount: number;
  sentAtUtc: string | null;
  lastError: string | null;
}

export interface PlanApprovalNotificationDetail extends PlanApprovalNotificationListItem {
  approvedByName: string;
  projectCount: number;
  bankFunding: number;
  selfFunding: number;
  availableFunding: number;
  aiGenerationUsed: boolean;
  subject: string | null;
  recipients: PlanApprovalNotificationRecipient[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
