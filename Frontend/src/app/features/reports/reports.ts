import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import {
  REPORT_KEYS,
  ReportCatalogItem,
  ReportKey,
} from '../../core/models/report.models';
import { FinancialYear } from '../../core/models/project.models';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { ReportsService } from '../../core/services/reports.service';
import { ToastService } from '../../core/services/toast.service';

interface ReportDefinition {
  key: ReportKey;
  title: string;
  description: string;
  category: string;
  includedFields: string[];
  icon: string;
}

const STATIC_REPORTS: readonly ReportDefinition[] = [
  {
    key: 'project-register',
    title: 'سجل المشروعات الشامل',
    description: 'كشف تفصيلي موحّد لكل المشروعات وبياناتها الأساسية والتنظيمية والمكانية.',
    category: 'المشروعات',
    includedFields: ['الأكواد', 'البرامج', 'نوع المشروع', 'الموقع', 'جهة التنفيذ', 'الحالة'],
    icon: 'M4 4h16v16H4zM8 8h8M8 12h8M8 16h5',
  },
  {
    key: 'funding-vs-spending',
    title: 'التمويل مقابل الصرف',
    description: 'تحليل مالي يقارن التمويل المعتمد بالمنصرف الفعلي ونسب الاستفادة على مستوى كل مشروع.',
    category: 'التمويل',
    includedFields: ['تمويل بنكي', 'تمويل ذاتي', 'إجمالي التمويل', 'إجمالي الصرف', 'نسبة الصرف', 'المتبقي'],
    icon: 'M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H7',
  },
  {
    key: 'bank-availability-ledger',
    title: 'سجل الإتاحات البنكية',
    description: 'سجل الإتاحات المستلمة من البنك مع التواريخ والرصيد التراكمي والمتبقي لكل سنة.',
    category: 'الإتاحات',
    includedFields: ['قيمة الإتاحة', 'تاريخ التسجيل', 'تاريخ الاستلام', 'الرصيد التراكمي', 'المتبقي', 'عدد الإثباتات'],
    icon: 'M3 10h18M5 10V7l7-4 7 4v3M6 10v8M10 10v8M14 10v8M18 10v8M3 21h18',
  },
  {
    key: 'plan-approval-status',
    title: 'موقف الخطط والاعتمادات',
    description: 'كشف بحالة الخطط والمشروعات المقترحة والمعتمدة وتواريخ الاعتماد وموقف كل مشروع.',
    category: 'التخطيط',
    includedFields: ['اسم الخطة', 'السنة المالية', 'حالة الخطة', 'المشروعات المقترحة', 'المشروعات المعتمدة', 'تاريخ الاعتماد'],
    icon: 'M4 5h16v16H4zM8 3v4M16 3v4M8 12l2 2 5-5',
  },
  {
    key: 'procurement-pipeline',
    title: 'مسار الطرح والتعاقد',
    description: 'تقرير زمني عن اكتمال مراحل الطرح الست وموقف التعاقد والتسليم لكل مشروع.',
    category: 'الإدارة المالية',
    includedFields: ['كراسة الشروط', 'الإعلان', 'فتح المظاريف', 'التقييمات', 'الإسناد', 'التعاقد'],
    icon: 'M9 11l3 3L22 4M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11',
  },
  {
    key: 'contracts-contractors',
    title: 'العقود والمقاولون',
    description: 'سجل تفصيلي بالعقود المسندة والمقاولين وقيم الأعمال والمدد والغرامات المسجلة.',
    category: 'العقود',
    includedFields: ['المقاول', 'نوع العقد', 'رقم العقد', 'قيمة العقد', 'مدة التنفيذ', 'الغرامات'],
    icon: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM22 21v-2a4 4 0 0 0-3-3.9',
  },
  {
    key: 'execution-delays',
    title: 'تأخيرات التنفيذ والتعثر',
    description: 'كشف رقابي بالمشروعات ومراحل التنفيذ التي تجاوزت مواعيدها مع أسباب التعثر ومدته.',
    category: 'الرقابة',
    includedFields: ['نسبة التنفيذ', 'حالة المشروع', 'ملاحظات المرحلة', 'المرحلة المتأخرة', 'أيام التأخير', 'الغرامات'],
    icon: 'M12 9v4m0 4h.01M10.3 3.7 2-1.2 2 1.2 7 12.1A2 2 0 0 1 19.3 19H4.7A2 2 0 0 1 3 15.8z',
  },
  {
    key: 'geographic-distribution',
    title: 'التوزيع الجغرافي للمشروعات',
    description: 'عرض مكاني للمشروعات والتمويل والتنفيذ موزعًا على المحافظات والمراكز.',
    category: 'التوزيع المكاني',
    includedFields: ['المحافظة', 'المركز', 'عدد المشروعات', 'إجمالي التمويل', 'إجمالي المصروف', 'متوسط التنفيذ'],
    icon: 'M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0ZM12 7a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z',
  },
  {
    key: 'program-agency-performance',
    title: 'أداء البرامج وجهات التنفيذ',
    description: 'مقارنة أداء البرامج الرئيسية والفرعية والجهات المنفذة من حيث التمويل والإنجاز.',
    category: 'تحليل الأداء',
    includedFields: ['البرنامج الرئيسي', 'البرنامج الفرعي', 'جهة التنفيذ', 'عدد المشروعات', 'إجمالي التمويل', 'متوسط التنفيذ'],
    icon: 'M4 5h16M4 12h10M4 19h7M18 15v6M15 18h6',
  },
  {
    key: 'measurements-outcomes',
    title: 'القياسات ومخرجات المشروعات',
    description: 'تقرير بالقياسات الكمية المسجلة ووحداتها وربطها ببيانات وتمويل كل مشروع.',
    category: 'المخرجات',
    includedFields: ['اسم المشروع', 'البرنامج الفرعي', 'القياس', 'الوحدة', 'القيمة المسجلة', 'إجمالي التمويل'],
    icon: 'M4 19V5M4 19h16M8 15l3-4 3 2 5-7M17 6h2v2',
  },
] as const;

const REPORT_KEY_SET = new Set<string>(REPORT_KEYS);
const MAX_AI_PROMPT_LENGTH = 500;

@Component({
  selector: 'app-reports',
  imports: [FormsModule],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class Reports {
  private readonly reportsService = inject(ReportsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly toast = inject(ToastService);

  protected readonly reportCards = signal<ReportDefinition[]>([...STATIC_REPORTS]);
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly downloadingKeys = signal<ReadonlySet<ReportKey>>(new Set());
  protected readonly aiPrompt = signal('');
  protected readonly aiGenerating = signal(false);

  protected readonly maxAiPromptLength = MAX_AI_PROMPT_LENGTH;
  protected readonly examples = [
    'اعمل تقرير سجل المشروعات المعتمدة فقط في مركز أشمون، مرتبًا من الأعلى تمويلًا.',
    'اعمل تقرير بمشروعات المقاولات وحالتها قيد التنفيذ، مرتبًا حسب اسم المشروع.',
    'اعمل تقرير موقف مراحل الطرح والتعاقد لمشروعات المقاولات في مركز منوف.',
    'اعمل تقرير الإتاحات البنكية للسنة المختارة مرتبًا من الأحدث إلى الأقدم.',
  ];

  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly yearScopeLabel = computed(() => {
    const selectedId = this.selectedYearId();
    if (selectedId == null) return 'كل السنوات';
    return this.financialYears().find((year) => year.id === selectedId)?.name ?? 'السنة المختارة';
  });
  protected readonly promptLength = computed(() => this.aiPrompt().trim().length);
  protected readonly canGenerateAi = computed(
    () =>
      this.promptLength() >= 8 &&
      this.promptLength() <= MAX_AI_PROMPT_LENGTH &&
      !this.aiGenerating(),
  );

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    let catalogFailed = false;
    let yearsFailed = false;

    forkJoin({
      catalog: this.reportsService.getCatalog().pipe(
        catchError(() => {
          catalogFailed = true;
          return of([] as ReportCatalogItem[]);
        }),
      ),
      years: this.financialYearsService.getAll().pipe(
        catchError(() => {
          yearsFailed = true;
          return of([] as FinancialYear[]);
        }),
      ),
    }).subscribe(({ catalog, years }) => {
      this.reportCards.set(this.mergeCatalog(catalog));
      this.financialYears.set(years);
      if (years.length > 0) {
        this.selectedYearId.set(
          this.financialYearsService.resolveSelectedYearId(years, this.selectedYearId()),
        );
      } else {
        this.selectedYearId.set(null);
      }

      if (catalogFailed && yearsFailed) {
        this.loadError.set('تعذر تحديث قائمة التقارير والسنوات المالية. يمكنك استخدام التقارير الثابتة لكل السنوات.');
      } else if (catalogFailed) {
        this.loadError.set('تعذر تحديث أوصاف التقارير من الخادم، لذلك يتم عرض القائمة الافتراضية.');
      } else if (yearsFailed) {
        this.loadError.set('تعذر تحميل السنوات المالية. سيتم إنشاء التقارير لكل السنوات.');
      }
      this.loading.set(false);
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    if (id != null) {
      this.financialYearsService.rememberSelectedYearId(id);
    }
  }

  protected download(report: ReportDefinition): void {
    if (this.isDownloading(report.key) || this.aiGenerating()) return;

    this.actionError.set(null);
    this.setDownloading(report.key, true);
    this.reportsService
      .downloadReport(report.key, this.selectedYearId())
      .pipe(finalize(() => this.setDownloading(report.key, false)))
      .subscribe({
        next: (response) => {
          try {
            this.reportsService.saveResponse(
              response,
              `${report.key}-${this.yearScopeLabel()}-${this.todayKey()}`,
            );
            this.toast.success(`تم إنشاء تقرير «${report.title}» وتنزيله بنجاح`);
          } catch {
            this.showActionError('تم إنشاء التقرير لكن تعذر تنزيل الملف');
          }
        },
        error: (error) => {
          void this.handleRequestError(error, `تعذر إنشاء تقرير «${report.title}»`);
        },
      });
  }

  protected useExample(example: string): void {
    this.aiPrompt.set(example);
    this.actionError.set(null);
  }

  protected generateAiReport(): void {
    const prompt = this.aiPrompt().trim();
    if (prompt.length < 8) {
      this.showActionError('اكتب وصفًا أوضح للتقرير المطلوب');
      return;
    }
    if (prompt.length > MAX_AI_PROMPT_LENGTH || this.aiGenerating() || this.downloadingKeys().size > 0) {
      return;
    }

    this.actionError.set(null);
    this.aiGenerating.set(true);
    this.reportsService
      .generateAiReport({ prompt, financialYearId: this.selectedYearId() })
      .pipe(finalize(() => this.aiGenerating.set(false)))
      .subscribe({
        next: (response) => {
          try {
            this.reportsService.saveResponse(
              response,
              `ai-report-${this.yearScopeLabel()}-${this.todayKey()}`,
            );
            this.toast.success('تم تجهيز التقرير الذكي وتنزيله بصيغة Excel');
          } catch {
            this.showActionError('تم إنشاء التقرير لكن تعذر تنزيل الملف');
          }
        },
        error: (error) => {
          void this.handleRequestError(error, 'تعذر فهم التقرير المطلوب أو إنشاؤه');
        },
      });
  }

  protected isDownloading(key: ReportKey): boolean {
    return this.downloadingKeys().has(key);
  }

  private setDownloading(key: ReportKey, downloading: boolean): void {
    const next = new Set(this.downloadingKeys());
    if (downloading) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this.downloadingKeys.set(next);
  }

  private mergeCatalog(catalog: ReportCatalogItem[]): ReportDefinition[] {
    const remoteByKey = new Map(
      catalog
        .filter((item) => REPORT_KEY_SET.has(item.key))
        .map((item) => [item.key, item] as const),
    );

    return STATIC_REPORTS.map((fallback) => {
      const remote = remoteByKey.get(fallback.key);
      const remoteFields = remote?.includedFields?.filter(
        (field) => typeof field === 'string' && field.trim().length > 0,
      );
      return {
        ...fallback,
        title: remote?.title?.trim() || fallback.title,
        description: remote?.description?.trim() || fallback.description,
        includedFields: remoteFields?.length ? remoteFields.slice(0, 8) : fallback.includedFields,
      };
    });
  }

  private async handleRequestError(error: unknown, fallback: string): Promise<void> {
    const message = await this.reportsService.errorMessage(error, fallback);
    this.showActionError(message);
  }

  private showActionError(message: string): void {
    this.actionError.set(message);
    this.toast.error(message);
  }

  private todayKey(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
