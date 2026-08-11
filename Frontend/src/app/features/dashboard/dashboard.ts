import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import * as echarts from 'echarts/core';
import { BarChart, GaugeChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components';
import { LabelLayout } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';
import type { ComposeOption } from 'echarts/core';
import type { BarSeriesOption, GaugeSeriesOption, LineSeriesOption, PieSeriesOption } from 'echarts/charts';
import type { GridComponentOption, LegendComponentOption, TooltipComponentOption } from 'echarts/components';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { DashboardOverview } from '../../core/models/dashboard.models';
import { FinancialYear } from '../../core/models/project.models';
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
  TooltipComponent,
  LabelLayout,
  CanvasRenderer,
]);

type EChartsOption = ComposeOption<
  BarSeriesOption | GaugeSeriesOption | LineSeriesOption | PieSeriesOption | GridComponentOption | LegendComponentOption | TooltipComponentOption
>;

const PALETTE = ['#1C7049', '#C79A3A', '#2E6FB0', '#DB4657', '#C98A12', '#269560', '#8A6512', '#15603F', '#B4872C', '#0F4A34'];

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

  protected readonly hasFundingData = computed(() => (this.overview()?.financialMetrics.totalFunding ?? 0) > 0);
  protected readonly hasStatusData = computed(() => (this.overview()?.projectMetrics.totalSubProjects ?? 0) > 0);
  protected readonly hasProgramData = computed(() => (this.overview()?.charts.programFunding.length ?? 0) > 0);
  protected readonly hasTimelineData = computed(() => (this.overview()?.charts.availabilityTimeline.length ?? 0) > 0);
  protected readonly hasMarkazData = computed(() => (this.overview()?.charts.markazDistribution.length ?? 0) > 0);
  protected readonly hasPriorityData = computed(() => (this.overview()?.charts.priorityDistribution.length ?? 0) > 0);
  protected readonly hasProgressData = computed(() =>
    (this.overview()?.charts.progressDistribution.reduce((a, x) => a + x.value, 0) ?? 0) > 0,
  );

  constructor() {
    this.loadFinancialYears();
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
    this.load();
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
  }

  private renderChart(key: string, container: HTMLElement, option: EChartsOption): void {
    let instance = this.charts.get(key);
    if (!instance || instance.isDisposed()) {
      instance = echarts.init(container, undefined, { renderer: 'canvas' });
      this.charts.set(key, instance);
      container.dataset['chartKey'] = key;
      this.resizeObserver?.observe(container);
    }
    instance.setOption(option, true);
  }

  // ===== خيارات كل مخطط =====
  private buildFundingOption(data: DashboardOverview): EChartsOption {
    const { fundingDistribution } = data.charts;
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط دائري يوضح توزيع التمويل بين البنكي والذاتي' },
      tooltip: { trigger: 'item', valueFormatter: (v) => this.chartThousandsLabel(v as number) },
      legend: { bottom: 0, textStyle: { fontFamily: 'Tajawal' } },
      color: [PALETTE[0], PALETTE[1]],
      series: [
        {
          type: 'pie',
          radius: ['52%', '75%'],
          center: ['50%', '44%'],
          avoidLabelOverlap: true,
          itemStyle: { borderColor: '#fff', borderWidth: 2 },
          label: { formatter: '{b}\n{d}%', fontFamily: 'Tajawal', fontSize: 11.5 },
          data: fundingDistribution.map((d) => ({ name: d.name, value: this.thousandsNumber(d.value) })),
        },
      ],
    };
  }

  private buildAvailabilityGaugeOption(data: DashboardOverview): EChartsOption {
    const rate = Math.min(100, Math.max(0, data.financialMetrics.availabilityRateOfBankFunding));
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
          progress: { show: true, width: 16, itemStyle: { color: PALETTE[0] } },
          axisLine: { lineStyle: { width: 16, color: [[1, '#EEF2EE']] } },
          axisTick: { show: false },
          splitLine: { show: false },
          axisLabel: { show: false },
          pointer: { show: false },
          detail: {
            valueAnimation: true,
            formatter: '{value}%',
            fontSize: 24,
            fontFamily: 'Cairo',
            color: '#0C3B2A',
            offsetCenter: [0, '10%'],
          },
          data: [{ value: Math.round(rate * 100) / 100 }],
        },
      ],
    };
  }

  private buildStatusOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.statusDistribution;
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
          itemStyle: { color: PALETTE[0], borderRadius: [8, 8, 0, 0] },
          data: items.map((i, idx) => ({ value: i.value, itemStyle: { color: PALETTE[idx % PALETTE.length] } })),
        },
      ],
    };
  }

  private buildProgramFundingOption(data: DashboardOverview): EChartsOption {
    const items = [...data.charts.programFunding].sort((a, b) => a.totalFunding - b.totalFunding);
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
        { name: 'بنكي', type: 'bar', stack: 'funding', itemStyle: { color: PALETTE[0] }, data: items.map((i) => this.thousandsNumber(i.bankFunding)) },
        { name: 'ذاتي', type: 'bar', stack: 'funding', itemStyle: { color: PALETTE[1] }, data: items.map((i) => this.thousandsNumber(i.selfFunding)) },
      ],
    };
  }

  private buildTimelineOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.availabilityTimeline;
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
          lineStyle: { color: PALETTE[0], width: 3 },
          itemStyle: { color: PALETTE[0] },
          areaStyle: { color: 'rgba(28,112,73,0.12)' },
          data: items.map((i) => this.thousandsNumber(i.cumulativeAmount)),
        },
      ],
    };
  }

  private buildMarkazOption(data: DashboardOverview): EChartsOption {
    const items = [...data.charts.markazDistribution].sort((a, b) => a.value - b.value).slice(-10);
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
          itemStyle: { color: PALETTE[2], borderRadius: [0, 6, 6, 0] },
          data: items.map((i) => i.value),
        },
      ],
    };
  }

  private buildPriorityOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.priorityDistribution;
    return {
      animation: false,
      aria: { enabled: true, description: 'مخطط وردي يوضح توزيع المشروعات حسب الأولوية' },
      tooltip: { trigger: 'item' },
      legend: { bottom: 0, textStyle: { fontFamily: 'Tajawal' } },
      color: PALETTE,
      series: [
        {
          type: 'pie',
          radius: ['20%', '75%'],
          center: ['50%', '44%'],
          roseType: 'radius',
          itemStyle: { borderColor: '#fff', borderWidth: 2 },
          label: { fontFamily: 'Tajawal', fontSize: 11 },
          data: items.map((i) => ({ name: i.name, value: i.value })),
        },
      ],
    };
  }

  private buildProgressOption(data: DashboardOverview): EChartsOption {
    const items = data.charts.progressDistribution;
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
          itemStyle: { color: PALETTE[5], borderRadius: [8, 8, 0, 0] },
          data: items.map((i) => i.value),
        },
      ],
    };
  }
}
