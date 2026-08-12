using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Domain.Common;

namespace SmartInvest.Application.Services;

public class PlanService : IPlanService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IPlanRepo planRepo;
    private readonly ICurrentUserService currentUser;
    private readonly IPlanApprovalNotificationEnqueuer notificationEnqueuer;

    public PlanService(
        IUnitOfWork unitOfWork,
        IPlanRepo planRepo,
        ICurrentUserService currentUser,
        IPlanApprovalNotificationEnqueuer notificationEnqueuer)
    {
        this.unitOfWork = unitOfWork;
        this.planRepo = planRepo;
        this.currentUser = currentUser;
        this.notificationEnqueuer = notificationEnqueuer;
    }
    // Plans with filter
    public List<Plan>? GetPlansByNameAndStatus(PlanStatus? planStatus, string? planName)
    {
        return planRepo.GetPlanByStatusAndName(planStatus, planName);
    }
    public Plan GetPlanDetails(int planId)
    {
        return  planRepo.GetPlanWithProjectsById(planId)!;
    }
    public Plan GetCurrentPlan()
    {
        return planRepo.GetCurrentPlan()!;
    }
    public async Task AddPlan(Plan plan)
    {
        if (plan.PlanStatus == PlanStatus.Approved)
        {
            throw new BusinessRuleException("يجب إنشاء الخطة كمقترحة ثم اعتمادها من إجراء اعتماد الخطة");
        }

        if (plan.PlanStatus == PlanStatus.Suggested)
        {
            var existing = await planRepo.FindAsync(p => p.FinancialYearId == plan.FinancialYearId && p.PlanStatus == PlanStatus.Suggested);
            if (existing.Any())
            {
                throw new BusinessRuleException("توجد بالفعل خطة مقترحة لهذه السنة المالية");
            }
        }

        await planRepo.AddAsync(plan);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task UpdatePlan(Plan plan)
    {
        planRepo.Update(plan);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task DeletePlan(Plan plan)
    {
        planRepo.Remove(plan);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task DeletePlanById(int planId)
    {
        var plan = await planRepo.GetByIdAsync(planId);
        if (plan != null)
        {
            planRepo.Remove(plan);
            await unitOfWork.SaveChangesAsync();
        }
    }
    ////////////// manage Projects in A Plan  ///////////////
    public async Task AddExistingProjectToPlan(int Planid, int ProjectId)
    {
        await planRepo.AddExistingProject(Planid, ProjectId);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task AddProjectToPlan(int Planid,SubProject project)
    {
        await planRepo.AddProject(Planid, project);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task DeleteProjectFromPlan(int PlanId, int ProjectId)
    {
        planRepo.DeleteProjectFromPlan(PlanId, ProjectId);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<Plan> ApproveAsync(int planId, DateTime approvalDate)
    {
        if (currentUser.Role is not Roles.PlanningManager and not Roles.SuperAdmin)
        {
            throw new ForbiddenAccessException("اعتماد الخطة يتطلب صلاحية مدير التخطيط");
        }

        if (approvalDate.Date > DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException("لا يمكن أن يكون تاريخ اعتماد الخطة في المستقبل");
        }

        var plan = planRepo.GetPlanWithProjectsById(planId);
        if (plan == null)
        {
            throw new NotFoundException($"الخطة رقم {planId} غير موجودة");
        }

        if (plan.ApprovalDate.HasValue || plan.PlanStatus == PlanStatus.Approved)
        {
            throw new BusinessRuleException("تم اعتماد هذه الخطة بالفعل");
        }

        if (plan.PlanProjects == null || plan.PlanProjects.Count == 0)
        {
            throw new BusinessRuleException("لا يمكن اعتماد خطة لا تحتوي على مشروعات");
        }

        var approvedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("تعذّر تحديد المستخدم الذي يعتمد الخطة");

        plan.ApprovalDate = approvalDate;
        plan.PlanStatus = PlanStatus.Approved;
        if (plan.PlanName.Contains("المقترحة", StringComparison.Ordinal))
        {
            plan.PlanName = plan.PlanName.Replace("المقترحة", "المعتمدة", StringComparison.Ordinal);
        }

        planRepo.Update(plan);
        await notificationEnqueuer.EnqueueAsync(plan, approvedByUserId);

        await unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            EntityName = nameof(Plan),
            EntityId = plan.PlanId,
            FieldName = "PlanStatus",
            OldValue = PlanStatus.Suggested.ToString(),
            NewValue = PlanStatus.Approved.ToString(),
            ChangedByUserId = approvedByUserId,
            ChangedAt = DateTime.UtcNow,
        });

        await unitOfWork.SaveChangesAsync();

        return plan;
    }
}
