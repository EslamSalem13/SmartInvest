import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FinancialYear } from '../../core/models/project.models';
import {
  PlanApprovalNotificationDetail,
  PlanApprovalNotificationListItem,
  PlanApprovalNotificationStatus,
  PlanApprovalRecipientStatus,
} from '../../core/models/plan-approval-notification.models';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { PlanApprovalNotificationsService } from '../../core/services/plan-approval-notifications.service';
import { ToastService } from '../../core/services/toast.service';
import { formatEgpAsThousands } from '../../core/utils/budget.util';

@Component({
  selector: 'app-plan-approval-notifications',
  imports: [DatePipe, FormsModule],
  templateUrl: './plan-approval-notifications.html',
  styleUrl: './plan-approval-notifications.css',
})
export class PlanApprovalNotifications {
  private readonly service = inject(PlanApprovalNotificationsService);
  private readonly yearsService = inject(FinancialYearsService);
  private readonly toast = inject(ToastService);

  protected readonly Status = PlanApprovalNotificationStatus;
  protected readonly RecipientStatus = PlanApprovalRecipientStatus;
  protected readonly items = signal<PlanApprovalNotificationListItem[]>([]);
  protected readonly years = signal<FinancialYear[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly statusFilter = signal<PlanApprovalNotificationStatus | null>(null);
  protected readonly yearFilter = signal<number | null>(null);
  protected readonly planNameFilter = signal('');
  protected readonly fromFilter = signal('');
  protected readonly toFilter = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly totalCount = signal(0);
  protected readonly detail = signal<PlanApprovalNotificationDetail | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly retryingId = signal<number | null>(null);

  constructor() {
    this.yearsService.getAll().subscribe({ next: (years) => this.years.set(years) });
    this.load();
  }

  protected load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    this.page.set(page);
    this.service.getAll(
      page,
      this.pageSize,
      this.statusFilter(),
      this.yearFilter(),
      this.planNameFilter(),
      this.fromFilter(),
      this.toFilter(),
    ).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'تعذّر تحميل سجل إشعارات اعتماد الخطط');
        this.loading.set(false);
      },
    });
  }

  protected openDetail(id: number): void {
    this.detailLoading.set(true);
    this.service.getById(id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.toast.error('تعذّر تحميل تفاصيل الإشعار');
      },
    });
  }

  protected closeDetail(): void {
    if (!this.detailLoading()) this.detail.set(null);
  }

  protected retry(item: PlanApprovalNotificationListItem | PlanApprovalNotificationDetail): void {
    if (this.retryingId() != null) return;
    this.retryingId.set(item.id);
    this.service.retry(item.id).subscribe({
      next: () => {
        this.retryingId.set(null);
        this.detail.set(null);
        this.toast.success('تمت جدولة إعادة الإرسال للمستلمين الذين فشل إرسالهم');
        this.load(this.page());
      },
      error: (err) => {
        this.retryingId.set(null);
        this.toast.error(err?.error?.message ?? 'تعذّرت جدولة إعادة الإرسال');
      },
    });
  }

  protected statusLabel(status: PlanApprovalNotificationStatus): string {
    return ({
      [PlanApprovalNotificationStatus.Pending]: 'بانتظار الإرسال',
      [PlanApprovalNotificationStatus.Processing]: 'جاري الإرسال',
      [PlanApprovalNotificationStatus.Sent]: 'تم الإرسال',
      [PlanApprovalNotificationStatus.PartiallyFailed]: 'إرسال جزئي',
      [PlanApprovalNotificationStatus.Failed]: 'فشل الإرسال',
      [PlanApprovalNotificationStatus.NoRecipients]: 'لا يوجد مستلمون',
    })[status];
  }

  protected recipientStatusLabel(status: PlanApprovalRecipientStatus): string {
    return status === PlanApprovalRecipientStatus.Sent
      ? 'تم الإرسال'
      : status === PlanApprovalRecipientStatus.Failed
        ? 'فشل'
        : 'بانتظار الإرسال';
  }

  protected statusClass(status: PlanApprovalNotificationStatus): string {
    if (status === PlanApprovalNotificationStatus.Sent) return 'ok';
    if (status === PlanApprovalNotificationStatus.Failed || status === PlanApprovalNotificationStatus.NoRecipients) return 'bad';
    if (status === PlanApprovalNotificationStatus.PartiallyFailed) return 'warn';
    return 'info';
  }

  protected roleLabel(role: string): string {
    return ({ FinancialManager: 'مدير الإدارة المالية', FinancialEmployee: 'موظف الإدارة المالية', SuperAdmin: 'السوبر أدمن' } as Record<string, string>)[role] ?? role;
  }

  protected money(value: number): string {
    return formatEgpAsThousands(value);
  }

  protected hasPrevious(): boolean { return this.page() > 1; }
  protected hasNext(): boolean { return this.page() * this.pageSize < this.totalCount(); }
}
