import { Component, computed, effect, inject, input, signal, viewChild, WritableSignal } from '@angular/core';
import { Observable } from 'rxjs';
import { LookupsService } from '../../core/services/lookups.service';
import { ContractTypesService } from '../../core/services/contract-types.service';
import { AuthService } from '../../core/services/auth.service';
import { Lookup, MarkazLookup, SubProgramLookup, VillageLookup } from '../../core/models/project.models';
import { SettingsLookupItem, SettingsLookupParentOption, SettingsLookupSaveEvent, SettingsLookupTable } from './settings-lookup-table';
import { SETTINGS_LOOKUP_TABS, TabKey } from './settings-tabs';

@Component({
  selector: 'app-settings-lookup-page',
  imports: [SettingsLookupTable],
  templateUrl: './settings-lookup-page.html',
})
export class SettingsLookupPage {
  private readonly lookups = inject(LookupsService);
  private readonly contractTypes = inject(ContractTypesService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.canManageProjects;

  readonly tab = input.required<TabKey>();

  private readonly lookupTable = viewChild.required<SettingsLookupTable>('lookupTable');

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly mainPrograms = signal<Lookup[]>([]);
  private readonly subPrograms = signal<SubProgramLookup[]>([]);
  private readonly governorates = signal<Lookup[]>([]);
  private readonly markazList = signal<MarkazLookup[]>([]);
  private readonly villages = signal<VillageLookup[]>([]);
  private readonly priorities = signal<Lookup[]>([]);
  private readonly statuses = signal<Lookup[]>([]);
  private readonly componentTypes = signal<Lookup[]>([]);
  private readonly projectLevels = signal<Lookup[]>([]);
  private readonly accountingUnits = signal<Lookup[]>([]);
  private readonly contractTypeList = signal<Lookup[]>([]);
  private readonly units = signal<Lookup[]>([]);

  protected readonly activeTabDef = computed(() => SETTINGS_LOOKUP_TABS.find((t) => t.key === this.tab())!);

  protected readonly parentOptions = computed<SettingsLookupParentOption[]>(() => {
    switch (this.tab()) {
      case 'subProgram':
        return this.mainPrograms().map((m) => ({ id: m.id, name: m.name }));
      case 'markaz':
        return this.governorates().map((g) => ({ id: g.id, name: g.name }));
      case 'village':
        return this.markazList().map((m) => ({ id: m.id, name: m.name }));
      default:
        return [];
    }
  });

  protected readonly items = computed<SettingsLookupItem[]>(() => {
    switch (this.tab()) {
      case 'mainProgram':
        return this.mainPrograms().map((m) => ({ id: m.id, name: m.name }));
      case 'subProgram':
        return this.subPrograms().map((s) => ({
          id: s.id,
          name: s.name,
          parentId: s.mainProgramId,
          parentName: this.mainPrograms().find((m) => m.id === s.mainProgramId)?.name ?? '',
        }));
      case 'governorate':
        return this.governorates().map((g) => ({ id: g.id, name: g.name }));
      case 'markaz':
        return this.markazList().map((m) => ({
          id: m.id,
          name: m.name,
          parentId: m.governorateId,
          parentName: this.governorates().find((g) => g.id === m.governorateId)?.name ?? '',
        }));
      case 'village':
        return this.villages().map((v) => ({
          id: v.id,
          name: v.name,
          parentId: v.markazId,
          parentName: this.markazList().find((m) => m.id === v.markazId)?.name ?? '',
        }));
      case 'priority':
        return this.priorities().map((p) => ({ id: p.id, name: p.name }));
      case 'status':
        return this.statuses().map((s) => ({ id: s.id, name: s.name }));
      case 'componentType':
        return this.componentTypes().map((c) => ({ id: c.id, name: c.name }));
      case 'projectLevel':
        return this.projectLevels().map((p) => ({ id: p.id, name: p.name }));
      case 'accountingUnit':
        return this.accountingUnits().map((a) => ({ id: a.id, name: a.name }));
      case 'contractType':
        return this.contractTypeList().map((c) => ({ id: c.id, name: c.name }));
      case 'unit':
        return this.units().map((u) => ({ id: u.id, name: u.name }));
      default:
        return [];
    }
  });

  constructor() {
    effect(() => {
      this.tab();
      this.loadAll();
    });
  }

  /** The active tab's own primary list — always loaded. */
  private ownLoadTask(tab: TabKey): { obs: Observable<any>; target: WritableSignal<any> } {
    switch (tab) {
      case 'mainProgram':
        return { obs: this.lookups.getMainPrograms(), target: this.mainPrograms };
      case 'subProgram':
        return { obs: this.lookups.getSubPrograms(), target: this.subPrograms };
      case 'governorate':
        return { obs: this.lookups.getGovernorates(), target: this.governorates };
      case 'markaz':
        return { obs: this.lookups.getMarkaz(), target: this.markazList };
      case 'village':
        return { obs: this.lookups.getVillages(), target: this.villages };
      case 'priority':
        return { obs: this.lookups.getPriorities(), target: this.priorities };
      case 'status':
        return { obs: this.lookups.getStatuses(), target: this.statuses };
      case 'componentType':
        return { obs: this.lookups.getComponentTypes(), target: this.componentTypes };
      case 'projectLevel':
        return { obs: this.lookups.getProjectLevels(), target: this.projectLevels };
      case 'accountingUnit':
        return { obs: this.lookups.getAccountingUnits(), target: this.accountingUnits };
      case 'contractType':
        return { obs: this.contractTypes.getAll(), target: this.contractTypeList };
      case 'unit':
        return { obs: this.lookups.getUnits(), target: this.units };
    }
  }

  /** The list backing the parent picker, for tabs whose TabDef has hasParent === true. */
  private parentLoadTask(tab: TabKey): { obs: Observable<any>; target: WritableSignal<any> } | null {
    switch (tab) {
      case 'subProgram':
        return { obs: this.lookups.getMainPrograms(), target: this.mainPrograms };
      case 'markaz':
        return { obs: this.lookups.getGovernorates(), target: this.governorates };
      case 'village':
        return { obs: this.lookups.getMarkaz(), target: this.markazList };
      default:
        return null;
    }
  }

  private loadAll(): void {
    this.loading.set(true);
    this.error.set(null);

    const tab = this.tab();
    const tasks = [this.ownLoadTask(tab)];
    if (this.activeTabDef().hasParent) {
      const parentTask = this.parentLoadTask(tab);
      if (parentTask) tasks.push(parentTask);
    }

    Promise.all(tasks.map(({ obs, target }) => this.toPromise(obs, target)))
      .then(() => this.loading.set(false))
      .catch(() => {
        this.loading.set(false);
        this.error.set('تعذّر تحميل الإعدادات');
      });
  }

  private toPromise<T>(obs: Observable<T>, target: WritableSignal<T>): Promise<void> {
    return new Promise((resolve, reject) => {
      obs.subscribe({ next: (v) => { target.set(v); resolve(); }, error: reject });
    });
  }

  protected onSave(event: SettingsLookupSaveEvent): void {
    const tab = this.tab();
    const req = this.buildSaveRequest(tab, event);
    if (!req) return;
    req.subscribe({
      next: () => {
        this.lookupTable().saveSucceeded();
        this.loadAll();
      },
      error: (err) => this.lookupTable().saveFailed(err?.error?.message ?? 'تعذّر الحفظ'),
    });
  }

  private buildSaveRequest(tab: TabKey, event: SettingsLookupSaveEvent) {
    const name = event.name;
    switch (tab) {
      case 'mainProgram':
        return event.id
          ? this.lookups.updateMainProgram(event.id, { name })
          : this.lookups.createMainProgram({ name });
      case 'subProgram': {
        const mainProgramId = event.parentId!;
        return event.id
          ? this.lookups.updateSubProgram(event.id, { name, mainProgramId })
          : this.lookups.createSubProgram({ name, mainProgramId });
      }
      case 'governorate':
        return event.id
          ? this.lookups.updateGovernorate(event.id, { name })
          : this.lookups.createGovernorate({ name });
      case 'markaz': {
        const governorateId = event.parentId!;
        return event.id
          ? this.lookups.updateMarkaz(event.id, { name, governorateId })
          : this.lookups.createMarkaz({ name, governorateId });
      }
      case 'village': {
        const markazId = event.parentId!;
        return event.id
          ? this.lookups.updateVillage(event.id, { name, markazId })
          : this.lookups.createVillage({ name, markazId });
      }
      case 'priority':
        return event.id
          ? this.lookups.updatePriority(event.id, { name })
          : this.lookups.createPriority({ name });
      case 'status':
        return event.id
          ? this.lookups.updateStatus(event.id, { name })
          : this.lookups.createStatus({ name });
      case 'componentType':
        return event.id
          ? this.lookups.updateComponentType(event.id, { name })
          : this.lookups.createComponentType({ name });
      case 'projectLevel':
        return event.id
          ? this.lookups.updateProjectLevel(event.id, { name })
          : this.lookups.createProjectLevel({ name });
      case 'accountingUnit':
        return event.id
          ? this.lookups.updateAccountingUnit(event.id, { name })
          : this.lookups.createAccountingUnit({ name });
      case 'contractType':
        return event.id
          ? this.contractTypes.update(event.id, { name })
          : this.contractTypes.create({ name });
      case 'unit':
        return event.id
          ? this.lookups.updateUnit(event.id, { name })
          : this.lookups.createUnit({ name });
      default:
        return null;
    }
  }

  protected onDelete(id: number): void {
    const tab = this.tab();
    const req = this.buildDeleteRequest(tab, id);
    req.subscribe({
      next: () => this.loadAll(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر الحذف'),
    });
  }

  private buildDeleteRequest(tab: TabKey, id: number) {
    switch (tab) {
      case 'mainProgram': return this.lookups.deleteMainProgram(id);
      case 'subProgram': return this.lookups.deleteSubProgram(id);
      case 'governorate': return this.lookups.deleteGovernorate(id);
      case 'markaz': return this.lookups.deleteMarkaz(id);
      case 'village': return this.lookups.deleteVillage(id);
      case 'priority': return this.lookups.deletePriority(id);
      case 'status': return this.lookups.deleteStatus(id);
      case 'componentType': return this.lookups.deleteComponentType(id);
      case 'projectLevel': return this.lookups.deleteProjectLevel(id);
      case 'accountingUnit': return this.lookups.deleteAccountingUnit(id);
      case 'contractType': return this.contractTypes.delete(id);
      case 'unit': return this.lookups.deleteUnit(id);
    }
  }
}
