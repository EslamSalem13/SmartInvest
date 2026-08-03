/** مفاتيح الصلاحيات — مطابقة لـ Backend/SmartInvest.Domain/Common/Permissions.cs */
export const Perm = {
  DashboardView: 'dashboard.view',

  ProjectsView: 'projects.view',
  ProjectsCreate: 'projects.create',
  ProjectsEdit: 'projects.edit',
  ProjectsDelete: 'projects.delete',
  ProjectsApprove: 'projects.approve',

  PlansView: 'plans.view',
  PlansManage: 'plans.manage',

  FinancialYearsManage: 'financialyears.manage',

  ContractorsView: 'contractors.view',
  ContractorsManage: 'contractors.manage',

  AgenciesView: 'agencies.view',
  AgenciesManage: 'agencies.manage',

  FinancialView: 'financial.view',
  FinancialUpload: 'financial.upload',
  FinancialComplete: 'financial.complete',

  MemosView: 'memos.view',
  MemosManage: 'memos.manage',

  UsersView: 'users.view',
  UsersManage: 'users.manage',

  RolesManage: 'roles.manage',
} as const;

export interface PermissionItem {
  key: string;
  label: string;
}

export interface PermissionGroup {
  key: string;
  label: string;
  items: PermissionItem[];
}

export interface AppRole {
  id: string;
  name: string;
  displayName: string;
  isSystem: boolean;
  userCount: number;
  permissionCount: number;
  createdAt: string;
}

export interface RoleDetail extends AppRole {
  permissions: string[];
}

export interface SaveRole {
  displayName: string;
  permissions: string[];
}
