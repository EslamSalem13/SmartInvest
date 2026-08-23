import { Component, ElementRef, OnDestroy, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import * as echarts from 'echarts/core';
import { BarChart, GaugeChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, LegendComponent, MarkLineComponent, TooltipComponent } from 'echarts/components';
import { LabelLayout } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';
import type { ComposeOption } from 'echarts/core';
import type { BarSeriesOption, GaugeSeriesOption, LineSeriesOption, PieSeriesOption } from 'echarts/charts';
import type { GridComponentOption, LegendComponentOption, MarkLineComponentOption, TooltipComponentOption } from 'echarts/components';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { FollowUpService } from '../../core/services/follow-up.service';
import { ProjectsService } from '../../core/services/projects.service';
import { ThemeService } from '../../core/services/theme.service';
import { DashboardOverview } from '../../core/models/dashboard.models';
import { ExecutionTimeline } from '../../core/models/follow-up.models';
import { FinancialYear, SubProjectListItem } from '../../core/models/project.models';
import { egpToThousands, formatEgpAsThousands } from '../../core/utils/budget.util';

// LabelLayout يلزم صراحةً مع الاستيراد الانتقائي من echarts/core حتى تتموضع تسميات
// القطاعات (pie/rose) بلا تراكب — غير مُضمَّنة تلقائيًا خارج الحزمة الكاملة echarts/index.
echarts.use([
  BarChart,
  GaugeChart,
  LineChart,
  PieChart,
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  TooltipComponent,
  LabelLayout,
  CanvasRenderer,
]);

type EChartsOption = ComposeOption<
  | BarSeriesOption | GaugeSeriesOption | LineSeriesOption | PieSeriesOption
  | GridComponentOption | LegendComponentOption | MarkLineComponentOption | TooltipComponentOption
>;

const PALETTE = ['#1C7049', '#C79A3A', '#2E6FB0', '#DB4657', '#C98A12', '#269560', '#8A6512', '#15603F', '#B4872C', '#0F4A34'];
const DARK_PALETTE = ['#2FA66A', '#D5AA4A', '#5CA6E8', '#F05A67', '#E2AE43', '#42B77B', '#F1D58A', '#27905F', '#BE9137', '#6BD39A'];

@Component({
  selector: 'app-dashboard',
  imports: [FormsModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly dashboardService = inject(DashboardService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly followUpService = inject(FollowUpService);
  private readonly projectsService = inject(ProjectsService);
  private readonly themeService = inject(ThemeService);

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  protected readonly overview = signal<DashboardOverview | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // كل الرسوم أدناه تُبنى بـ animation:false — لوحظ أن حركة الظهور الافتراضية (خصوصًا لقطاعات
  // pie/gauge) تترك أحيانًا startAngle=endAngle بعد انتهائها فتُخفي القطاع نهائيًا؛ إيقافها يضمن
  // عرض البيانات بصورة صحيحة دائمًا، وهذا يحترم prefers-reduced-motion تلقائيًا لأنه لا حركة أصلًا.
  private readonly charts = new Map<string, echarts.ECharts>();
  private readonly resizeObserver =
    typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver((entries) => {
          for (const entry of entries) {
            const key = (entry.target as HTMLElement).dataset['chartKey'];
            if (key) this.charts.get(key)?.resize();
          }
        })
      : null;

  @ViewChild('fundingChart') fundingChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('availabilityGaugeChart') availabilityGaugeChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('statusChart') statusChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('programFundingChart') programFundingChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('availabilityTimelineChart') availabilityTimelineChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('markazChart') markazChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('priorityChart') priorityChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('progressChart') progressChartEl?: ElementRef<HTMLDivElement>;
  @ViewChild('executionTimelineChart') executionTimelineChartEl?: ElementRef<HTMLDivElement>;

  protected readonly hasFundingData = computed(() => (this.overview()?.financialMetrics.totalFunding ?? 0) > 0);
  protected readonly hasStatusData = computed(() => (this.overview()?.projectMetrics.totalSubProjects ?? 0) > 0);
  protected readonly hasProgramData = computed(() => (this.overview()?.charts.programFunding.length ?? 0) > 0);
  protected readonly hasTimelineData = computed(() => (this.overview()?.charts.availabilityTimeline.length ?? 0) > 0);
  protected readonly hasMarkazData = computed(() => (this.overview()?.charts.markazDistribution.length ?? 0) > 0);
  protected readonly hasPriorityData = computed(() => (this.overview()?.charts.priorityDistribution.length ?? 0) > 0);
  protected readonly hasProgressData = computed(() =>
    (this.overview()?.charts.progressDistribution.reduce((a, x) => a + x.value, 0) ?? 0) > 0,
  );

  // ===== مخطط "تطور التنفيذ" — لكل مشروع على حدة، مستقل عن السنة المختارة في لوحة التحكم =====
  /** القائمة الكاملة لكل المشروعات الفرعية بلا تقييد سنة — مصدر بحث القائمة المنسدلة. تُجلب مرة واحدة. */
  protected readonly chartProjects = signal<SubProjectListItem[]>([]);
  protected readonly chartProjectQuery = signal('');
  protected readonly selectedChartProjectId = signal<number | null>(null);
  protected readonly executionTimeline = signal<ExecutionTimeline | null>(null);
  protected readonly timelineLoading = signal(false);
  protected readonly timelineError = signal<string | null>(null);

  protected readonly hasExecutionTimelineData = computed(() => (this.executionTimeline()?.points.length ?? 0) > 0);

  private readonly chartProjectLabelToId = computed(() => {
    const map = new Map<string, number>();
    for (const p of this.chartProjects()) {
      map.set(this.chartProjectLabel(p), p.id);
    }
    return map;
  });

  constructor() {
    effect(() => {
      this.themeService.theme();
      setTimeout(() => this.renderAllCharts(), 0);
    });
    this.loadFinancialYears();
    this.loadChartProjects();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    for (const chart of this.charts.values()) {
      chart.dispose();
    }
    this.charts.clear();
  }

  private loadFinancialYears(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        this.selectedYearId.set(
          this.financialYearsService.resolveSelectedYearId(years, this.selectedYearId()),
        );
        this.load();
      },
      error: () => this.load(),
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.dashboardService.getOverview(this.selectedYearId()).subscribe({
      next: (data) => {
        this.overview.set(data);
        this.selectedYearId.set(data.year.financialYearId);
        this.financialYearsService.rememberSelectedYearId(data.year.financialYearId);
        this.loading.set(false);
        setTimeout(() => this.renderAllCharts(), 0);
      },
      error: () => {
        this.error.set('تعذّر تحميل بيانات لوحة التحكم');
        this.loading.set(false);
      },
    });
  }

  protected onYearChange(id: number): void {
    this.selectedYearId.set(id);
    this.financialYearsService.rememberSelectedYearId(id);
    this.load();
  }

  // ===== مخطط "تطور التنفيذ" =====
  private loadChartProjects(): void {
    // بلا financialYearId عمدًا — قائمة البحث تغطي كل المشروعات بصرف النظر عن سنة لوحة التحكم المختارة،
    // فالمخطط نفسه يعرض عمر المشروع كله لا سنة واحدة.
    this.projectsService.searchSubProjects({ page: 1, pageSize: 1000 }).subscribe({
      next: (result) => this.chartProjects.set(result.items),
      error: () => this.chartProjects.set([]),
    });
  }

  protected chartProjectLabel(p: SubProjectListItem): string {
    return p.code ? `${p.name} (${p.code})` : p.name;
  }

  /** كل ضغطة حرف أثناء الكتابة — لا تُغيّر المخطط المعروض إلا عندما يطابق النص المكتوب مشروعًا فعليًا. */
  protected onChartProjectQueryChange(value: string): void {
    this.chartProjectQuery.set(value);
    const id = this.chartProjectLabelToId().get(value);
    if (id != null && id !== this.selectedChartProjectId()) {
      this.selectedChartProjectId.set(id);
      this.loadExecutionTimeline(id);
    }
  }

  private loadExecutionTimeline(subProjectId: number): void {
    this.timelineLoading.set(true);
    this.timelineError.set(null);
    this.followUpService.getExecutionTimeline(subProjectId).subscribe({
      next: (timeline) => {
        this.executionTimeline.set(timeline);
        this.timelineLoading.set(false);
        setTimeout(() => this.renderAllCharts(), 0);
      },
      error: () => {
        this.executionTimeline.set(null);
        this.timelineLoading.set(false);
        this.timelineError.set('تعذّر تحميل بيانات تنفيذ هذا المشروع');
      },
    });
  }

  // ===== تنسيق الأرقام =====
  protected money(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  protected thousandsLabel(value: number | null | undefined): string {
    return formatEgpAsThousands(value);
  }

  private thousandsNumber(value: number | null | undefined): number {
    return egpToThousands(value) ?? 0;
  }

  private chartThousandsLabel(value: number | string | null | undefined): string {
    const numericValue = typeof value === 'number' ? value : Number(value ?? 0);
    return `${numericValue.toLocaleString('en-US', { maximumFractionDigits: 3 })} ألف ج.م`;
  }

  protected percent(value: number | null | undefined): string {
    return `${(value ?? 0).toLocaleString('en-US', { maximumFractionDigits: 2 })}%`;
  }

  protected dateOnly(value: string | null | undefined): string {
    return value ? value.slice(0, 10) : '—';
  }

  protected isOverdue(deadline: string | null): boolean {
    if (!deadline) return false;
    return new Date(deadline) < new Date();
  }

  // ===== روابط الكروت والأقسام التفصيلية =====
  /** query params لصفحة المشروعات — تحمل السنة المختارة، وفلتر اعتماد اختياري، وفتح سجل الإتاحات. */
  protected projectsQueryParams(
    approval?: 'approved' | 'pending' | 'stalled',
    openAvailability?: boolean,
  ): Record<string, string> {
    const params: Record<string, string> = {};
    const yearId = this.selectedYearId();
    if (yearId != null) params['financialYearId'] = String(yearId);
    if (approval) params['approval'] = approval;
    if (openAvailability) params['openAvailability'] = 'true';
    return params;
  }

  /** query params لصفحة متابعة المشروعات — تحمل السنة المختارة. */
  protected followUpQueryParams(): Record<string, string> {
    const params: Record<string, string> = {};
    const yearId = this.selectedYearId();
    if (yearId != null) params['financialYearId'] = String(yearId);
    return params;
  }

  // ===== رسم كل المخططات =====
  private renderAllCharts(): void {
    const data = this.overview();
    if (!data) return;

    if (this.hasFundingData() && this.fundingChartEl) {
      this.renderChart('funding', this.fundingChartEl.nativeElement, this.buildFundingOption(data));
    }
    if (this.availabilityGaugeChartEl) {
      this.renderChart('availabilityGauge', this.availabilityGaugeChartEl.nativeElement, this.buildAvailabilityGaugeOption(data));
    }
    if (this.hasStatusData() && this.statusChartEl) {
      this.renderChart('status', this.statusChartEl.nativeElement, this.buildStatusOption(data));
    }
    if (this.hasProgramData() && this.programFundingChartEl) {
      this.renderChart('programFunding', this.programFundingChartEl.nativeElement, this.buildProgramFundingOption(data));
    }
    if (this.hasTimelineData() && this.availabilityTimelineChartEl) {
      this.renderChart('availabilityTimeline', this.availabilityTimelineChartEl.nativeElement, this.buildTimelineOption(data));
    }
    if (this.hasMarkazData() && this.markazChartEl) {
      this.renderChart('markaz', this.markazChartEl.nativeElement, this.buildMarkazOption(data));
    }
    if (this.hasPriorityData() && this.priorityChartEl) {
      this.renderChart('priority', this.priorityChartEl.nativeElement, this.buildPriorityOption(data));
    }
    if (this.hasProgressData() && this.progressChartEl) {
      this.renderChart('progress', this.progressChartEl.nativeElement, this.buildProgressOption(data));
    }

    const timeline = this.executionTimeline();
    if (timeline && this.hasExecutionTimelineData() && this.executionTimelineChartEl) {
      this.renderChart('executionTimeline', this.executionTimelineChartEl.nativeElement, this.buildExecutionTimelineOption(timeline));
    }
  }

  private renderChart(key: string, container: HTMLElement, option: EChartsOption): void {
    let instance = this.charts.get(key);
    if (!instance || instance.isDisposed()) {
      instance = echarts.init(container, undefined, { renderer: 'canvas' });
      this.charts.set(key, instance);
      container.dataset['chartKey'] = key;
      this.resizeObserver?.observe(container);
    }
    instance.setOption(this.withChartTheme(option), true);
  }

  private cssToken(name: string, fallback: string): string {
    if (typeof document === 'undefined') return fallback;
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
  }

  private chartPalette(): string[] {
    return this.themeService.isDark() ? DARK_PALETTE : PALETTE;
  }

  private withChartTheme(option: EChartsOption): EChartsOption {
    const themed = option as any;
    const text = this.cssToken('--ink', '#14201A');
    const muted = this.cssToken('--chart-text', '#647569');
    const border = this.cssToken('--line-strong', '#CDD8CC');
    const grid = this.cssToken('--chart-grid', '#E3E9E2');
    const surface = this.cssToken('--surface-2', '#F6F9F6');

    const themeAxis = (axis: any): any => {
      if (!axis) return axis;
      if (Array.isArray(axis)) return axis.map(themeAxis);
      return {
        ...axis,
        axisLabel: { color: muted, fontFamily: 'Tajawal', ...(axis.axisLabel ?? {}) },
        nameTextStyle: { color: muted, fontFamily: 'Tajawal', ...(axis.nameTextStyle ?? {}) },
        axisLine: { ...(axis.axisLine ?? {}), lineStyle: { color: border, ...(axis.axisLine?.lineStyle ?? {}) } },
        axisTick: { ...(axis.axisTick ?? {}), lineStyle: { color: border, ...(axis.axisTick?.lineStyle ?? {}) } },
        splitLine: { ...(axis.splitLine ?? {}), lineStyle: { color: grid, ...(axis.splitLine?.lineStyle ?? {}) } },
      };
    };

    return {
      ...themed,
      backgroundColor: 'transparent',
      textStyle: { color: text, fontFamily: 'Tajawal', ...(themed.textStyle ?? {}) },
      tooltip: themed.tooltip
        ? {
            backgroundColor: surface,
            borderColor: border,
            textStyle: { color: text, fontFamily: 'Tajawal' },
            ...themed.tooltip,
          }
        : themed.tooltip,
      legend: themed.legend
        ? {
            ...themed.legend,
            textStyle: { color: muted, fontFamily: 'Tajawal', ...(themed.legend.textStyle ?? {}) },
          }
        : themed.legend,
      xAxis: themeAxis(themed.xAxis),
      yAxis: themeAxis(themed.yAxis),
    } as EChartsOption;
  }

  // ===== خيارات كل مخطط =====
  private buildFundingOption(data: DashboardOverview): EChartsOption {
    const { fundingDistribution } = data.charts;
    const palette = this.chartPalette();
    const pieData = fundingDistribution.map((d, index) => ({
      name: d.name,
      value: this.thousandsNumber(d.value),
      itemStyle: {
        color: this.chartGradient(palette[index % palette.length], palette[(index + 2) % palette.length]),
      },
    }));
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط دائري يوضح توزيع التمويل بين البنكي والذاتي' },
      tooltip: { trigger: 'item', valueFormatter: (v) => this.chartThousandsLabel(v as number) },
      color: [palette[0], palette[1]],
      series: [
        {
          type: 'pie',
          silent: true,
          radius: ['52%', '75%'],
          center: ['50%', '53%'],
          label: { show: false },
          itemStyle: { color: this.chartDepthColor(), borderWidth: 0 },
          data: pieData.map((item) => ({ name: item.name, value: item.value })),
          z: 1,
        },
        {
          type: 'pie',
          radius: ['52%', '75%'],
          center: ['50%', '50%'],
          avoidLabelOverlap: true,
          itemStyle: {
            borderColor: this.cssToken('--surface', '#fff'),
            borderWidth: 2,
            shadowBlur: 16,
            shadowOffsetY: 8,
            shadowColor: this.chartShadowColor(),
          },
          label: { color: this.cssToken('--chart-text', '#647569'), formatter: '{b}\n{d}%', fontFamily: 'Tajawal', fontSize: 11.5 },
          data: pieData,
          z: 2,
        },
      ],
    };
  }

  private buildAvailabilityGaugeOption(data: DashboardOverview): EChartsOption {
    const rate = Math.min(100, Math.max(0, data.financialMetrics.availabilityRateOfBankFunding));
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مقياس دائري يوضح نسبة الإتاحات البنكية من إجمالي التمويل البنكي' },
      series: [
        {
          type: 'gauge',
          startAngle: 200,
          endAngle: -20,
          min: 0,
          max: 100,
          radius: '92%',
          center: ['50%', '58%'],
          progress: {
            show: true,
            width: 16,
            itemStyle: {
              color: this.chartGradient(palette[0], palette[5]),
              shadowBlur: 14,
              shadowOffsetY: 5,
              shadowColor: this.chartShadowColor(),
            },
          },
          axisLine: { lineStyle: { width: 16, color: [[1, this.cssToken('--chart-track', '#EEF2EE')]] } },
          axisTick: { show: false },
          splitLine: { show: false },
          axisLabel: { show: false },
          pointer: { show: false },
          detail: {
            valueAnimation: true,
            formatter: '{value}%',
            fontSize: 24,
            fontFamily: 'Cairo',
            color: this.cssToken('--heading', '#0C3B2A'),
            offsetCenter: [0, '10%'],
          },
          data: [{ value: Math.round(rate * 100) / 100 }],
        },
      ],
    };
  }

  private buildStatusOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.statusDistribution;
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط أعمدة يوضح توزيع المشروعات حسب الحالة' },
      grid: { top: 20, right: 12, bottom: 30, left: 12, containLabel: true },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      xAxis: { type: 'category', data: items.map((i) => i.name), axisLabel: { fontFamily: 'Tajawal', fontSize: 11.5 } },
      yAxis: { type: 'value' },
      series: [
        {
          type: 'bar',
          barMaxWidth: 40,
          showBackground: true,
          backgroundStyle: { color: this.cssToken('--chart-track', '#EEF2EE'), borderRadius: [8, 8, 0, 0] },
          itemStyle: { borderRadius: [8, 8, 0, 0], shadowBlur: 10, shadowOffsetY: 5, shadowColor: this.chartShadowColor() },
          data: items.map((i, idx) => ({
            value: i.value,
            itemStyle: { color: this.chartGradient(palette[idx % palette.length], palette[(idx + 2) % palette.length]) },
          })),
        },
      ],
    };
  }

  private buildProgramFundingOption(data: DashboardOverview): EChartsOption {
    const items = [...data.charts.programFunding].sort((a, b) => a.totalFunding - b.totalFunding);
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط أعمدة أفقي مكدّس يوضح التمويل البنكي والذاتي لكل برنامج رئيسي' },
      grid: { top: 20, right: 16, bottom: 12, left: 12, containLabel: true },
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        valueFormatter: (v) => this.chartThousandsLabel(v as number),
      },
      legend: { top: 0, textStyle: { fontFamily: 'Tajawal' } },
      xAxis: { type: 'value', name: 'ألف ج.م', nameTextStyle: { fontFamily: 'Tajawal' } },
      yAxis: {
        type: 'category',
        data: items.map((i) => i.programName),
        axisLabel: {
          fontFamily: 'Tajawal',
          fontSize: 11.5,
          width: 140,
          overflow: 'truncate',
        },
        triggerEvent: true,
      },
      series: [
        {
          name: 'بنكي', type: 'bar', stack: 'funding',
          itemStyle: { color: this.chartGradient(palette[0], palette[5], true), shadowBlur: 8, shadowOffsetY: 4, shadowColor: this.chartShadowColor() },
          data: items.map((i) => this.thousandsNumber(i.bankFunding)),
        },
        {
          name: 'ذاتي', type: 'bar', stack: 'funding',
          itemStyle: { color: this.chartGradient(palette[1], palette[4], true), borderRadius: [0, 7, 7, 0], shadowBlur: 8, shadowOffsetY: 4, shadowColor: this.chartShadowColor() },
          data: items.map((i) => this.thousandsNumber(i.selfFunding)),
        },
      ],
    };
  }

  private buildTimelineOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.availabilityTimeline;
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط خطي يوضح تراكم الإتاحات البنكية عبر الزمن' },
      grid: { top: 24, right: 20, bottom: 30, left: 12, containLabel: true },
      tooltip: {
        trigger: 'axis',
        valueFormatter: (v) => this.chartThousandsLabel(v as number),
      },
      xAxis: { type: 'category', data: items.map((i) => this.dateOnly(i.receivedDate)), axisLabel: { fontFamily: 'Tajawal', fontSize: 11 } },
      yAxis: { type: 'value', name: 'ألف ج.م', nameTextStyle: { fontFamily: 'Tajawal' } },
      series: [
        {
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 7,
          lineStyle: { color: palette[0], width: 4, shadowBlur: 12, shadowOffsetY: 5, shadowColor: this.chartShadowColor() },
          itemStyle: { color: palette[0], borderColor: this.cssToken('--surface', '#fff'), borderWidth: 2, shadowBlur: 8, shadowColor: palette[0] },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: this.themeService.isDark() ? 'rgba(47,166,106,.48)' : 'rgba(28,112,73,.34)' },
              { offset: 1, color: 'rgba(28,112,73,0)' },
            ]),
          },
          data: items.map((i) => this.thousandsNumber(i.cumulativeAmount)),
        },
      ],
    };
  }

  private buildMarkazOption(data: DashboardOverview): EChartsOption {
    const items = [...data.charts.markazDistribution].sort((a, b) => a.value - b.value).slice(-10);
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط أعمدة أفقي يوضح توزيع المشروعات حسب المركز' },
      grid: { top: 12, right: 20, bottom: 12, left: 12, containLabel: true },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      xAxis: { type: 'value' },
      yAxis: { type: 'category', data: items.map((i) => i.name), axisLabel: { fontFamily: 'Tajawal', fontSize: 11.5 } },
      series: [
        {
          type: 'bar',
          barMaxWidth: 18,
          showBackground: true,
          backgroundStyle: { color: this.cssToken('--chart-track', '#EEF2EE'), borderRadius: [0, 7, 7, 0] },
          itemStyle: { color: this.chartGradient(palette[2], palette[0], true), borderRadius: [0, 7, 7, 0], shadowBlur: 9, shadowOffsetY: 4, shadowColor: this.chartShadowColor() },
          data: items.map((i) => i.value),
        },
      ],
    };
  }

  private buildPriorityOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.priorityDistribution;
    const palette = this.chartPalette();
    const priorityData = items.map((item, index) => ({
      name: item.name,
      value: item.value,
      itemStyle: { color: this.chartGradient(palette[index % palette.length], palette[(index + 3) % palette.length]) },
    }));
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط وردي يوضح توزيع المشروعات حسب الأولوية' },
      tooltip: { trigger: 'item' },
      color: palette,
      series: [
        {
          type: 'pie',
          silent: true,
          radius: ['20%', '75%'],
          center: ['50%', '53%'],
          roseType: 'radius',
          label: { show: false },
          itemStyle: { color: this.chartDepthColor(), borderWidth: 0 },
          data: priorityData.map((item) => ({ name: item.name, value: item.value })),
          z: 1,
        },
        {
          type: 'pie',
          radius: ['20%', '75%'],
          center: ['50%', '50%'],
          roseType: 'radius',
          itemStyle: {
            borderColor: this.cssToken('--surface', '#fff'),
            borderWidth: 2,
            shadowBlur: 15,
            shadowOffsetY: 7,
            shadowColor: this.chartShadowColor(),
          },
          label: { color: this.cssToken('--chart-text', '#647569'), fontFamily: 'Tajawal', fontSize: 11 },
          data: priorityData,
          z: 2,
        },
      ],
    };
  }

  private buildProgressOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.progressDistribution;
    const palette = this.chartPalette();
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط أعمدة يوضح توزيع المشروعات حسب نطاق نسبة التنفيذ' },
      grid: { top: 20, right: 12, bottom: 30, left: 12, containLabel: true },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      xAxis: { type: 'category', data: items.map((i) => i.name), axisLabel: { fontFamily: 'Tajawal', fontSize: 11.5 } },
      yAxis: { type: 'value' },
      series: [
        {
          type: 'bar',
          barMaxWidth: 40,
          showBackground: true,
          backgroundStyle: { color: this.cssToken('--chart-track', '#EEF2EE'), borderRadius: [8, 8, 0, 0] },
          itemStyle: { borderRadius: [8, 8, 0, 0], shadowBlur: 10, shadowOffsetY: 5, shadowColor: this.chartShadowColor() },
          data: items.map((i) => ({
            value: i.value,
            itemStyle: {
              color: this.chartGradient(
                i.name === 'أكثر من 100%' ? palette[3] : i.name === '100%' ? palette[5] : palette[2],
                i.name === 'أكثر من 100%' ? palette[4] : i.name === '100%' ? palette[0] : palette[5],
              ),
            },
          })),
        },
      ],
    };
  }

  /**
   * نسبة التنفيذ العيني التراكمية مقابل نسبة الصرف التراكمية من قيمة العقد، عبر عمر المشروع كله.
   * سقفا مرجع أفقيان عبر markLine بدل سلسلتي بيانات إضافيتين: 100% (قيمة العقد) وسقف التجاوز الأقصى.
   */
  private buildExecutionTimelineOption(timeline: ExecutionTimeline): EChartsOption {
    const palette = this.chartPalette();
    const points = timeline.points;
    const values = points.flatMap((p) => [p.cumulativeProgressPercent, p.cumulativeSpendPercent]);
    const ceilings = [timeline.contractValueCeilingPercent, timeline.maxAllowedCeilingPercent].filter(
      (v): v is number => v != null,
    );
    const maxY = Math.max(100, ...ceilings, ...values);

    const ceilingMarkLine = (value: number | null, label: string, color: string) =>
      value == null
        ? undefined
        : {
            symbol: 'none' as const,
            silent: true,
            animation: false,
            label: { formatter: label, fontFamily: 'Tajawal', fontSize: 11, position: 'insideEndTop' as const },
            lineStyle: { color, type: 'dashed' as const, width: 1.5 },
            data: [{ yAxis: value }],
          };

    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط خطي يقارن نسبة التنفيذ العيني بنسبة الصرف من قيمة العقد عبر عمر المشروع' },
      grid: { top: 24, right: 20, bottom: 30, left: 12, containLabel: true },
      legend: { top: 0, textStyle: { fontFamily: 'Tajawal' } },
      tooltip: {
        trigger: 'axis',
        formatter: (params: unknown) => {
          const items = (Array.isArray(params) ? params : [params]) as Array<{
            dataIndex: number; marker: string; seriesName: string; data: number;
          }>;
          const point = points[items[0]?.dataIndex ?? -1];
          if (!point) return '';
          const rows = items.map((it) => `${it.marker} ${it.seriesName}: ${this.percent(it.data)}`).join('<br/>');
          return `${point.label} — ${this.dateOnly(point.date)}<br/>${rows}`;
        },
      },
      xAxis: {
        type: 'category',
        data: points.map((p) => this.dateOnly(p.date)),
        axisLabel: { fontFamily: 'Tajawal', fontSize: 11 },
      },
      yAxis: {
        type: 'value',
        name: '%',
        min: 0,
        max: Math.ceil(maxY * 1.1),
        nameTextStyle: { fontFamily: 'Tajawal' },
      },
      series: [
        {
          name: 'نسبة التنفيذ العيني',
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 7,
          lineStyle: { color: palette[0], width: 3 },
          itemStyle: { color: palette[0] },
          data: points.map((p) => p.cumulativeProgressPercent),
          markLine: ceilingMarkLine(timeline.contractValueCeilingPercent, 'قيمة العقد (100%)', palette[5]),
        },
        {
          name: 'نسبة الصرف من قيمة العقد',
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 7,
          lineStyle: { color: palette[3], width: 3 },
          itemStyle: { color: palette[3] },
          data: points.map((p) => p.cumulativeSpendPercent),
          markLine: ceilingMarkLine(
            timeline.maxAllowedCeilingPercent,
            `السقف الأقصى المسموح (${this.percent(timeline.maxAllowedCeilingPercent)})`,
            palette[4],
          ),
        },
      ],
    };
  }

  private chartGradient(from: string, to: string, horizontal = false): echarts.graphic.LinearGradient {
    return new echarts.graphic.LinearGradient(
      0,
      0,
      horizontal ? 1 : 0,
      horizontal ? 0 : 1,
      [
        { offset: 0, color: from },
        { offset: 1, color: to },
      ],
    );
  }

  private chartShadowColor(): string {
    return this.themeService.isDark() ? 'rgba(0, 0, 0, .42)' : 'rgba(12, 59, 42, .22)';
  }

  private chartDepthColor(): string {
    return this.themeService.isDark() ? 'rgba(0, 0, 0, .38)' : 'rgba(13, 66, 47, .18)';
  }
}
