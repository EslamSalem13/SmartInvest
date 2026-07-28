# SmartInvest — مرجع المشروع

هذا الملف مرجع شامل وقائم بذاته لمشروع SmartInvest — مفيد لأي حد (شخص أو AI) يبدأ يشتغل على المشروع من غير أي سياق سابق. حاول تحدّثه كل ما تتغيّر حاجة جوهرية في البنية أو الصلاحيات أو الـ API.

## نظرة عامة

SmartInvest نظام لإدارة خطة التنمية الاستثمارية لمحافظة المنوفية — بيتتبّع البرامج والمشروعات الرئيسية والفرعية عبر السنوات المالية، وبيدير سير عمل تعيين المقاولين والجهات التنفيذية على المشروعات، وبيؤرشف "خطط" (مقترحة/معتمدة) قابلة للطباعة.

الواجهة كلها بالعربية (RTL). كود المصدر (أسماء المتغيرات، التعليقات القليلة الموجودة) بالإنجليزية.

## البنية التقنية

- **الباك إند**: .NET 10، معمارية Onion (Domain → Application → Infrastructure → API)، Entity Framework Core + SQL Server، ASP.NET Core Identity + JWT، AutoMapper (جزئيًا — شوف "التناقض في نمط الـ Plan" تحت)، FluentValidation.
- **الفرونت إند**: Angular (أحدث إصدار، standalone components، بدون NgModules)، Signals للحالة (مفيش RxJS state management ولا getters عادية)، `FormsModule` + `[ngModel]`/`(ngModelChange)` (مفيش Reactive Forms).
- **قاعدة البيانات**: SQL Server محلي، `Server=.;Database=SmartInvestDB`.

## بنية الباك إند (Onion)

- `SmartInvest.Domain` — الكيانات (Entities)، الـ Enums، `Roles` (ثوابت الأدوار)، `IGenericRepository<T>`/`IUnitOfWork` (العقود فقط).
- `SmartInvest.Application` — الـ DTOs، الـ Services (منطق العمل)، الـ Validators، الـ Interfaces، `Common/Exceptions` (`NotFoundException`, `BusinessRuleException`, `ForbiddenAccessException`).
- `SmartInvest.Infrastructure` — `AppDbContext`، الـ Migrations، تطبيقات الـ Repositories، `IdentityService` (تسجيل الدخول/إدارة المستخدمين)، `DependencyInjection.cs`.
- `SmartInvest.API` — الـ Controllers، `Program.cs` (التسجيل + الـ pipeline + بذر البيانات الأولية)، `Middleware/ExceptionHandlingMiddleware` (بيحوّل الاستثناءات لأكواد HTTP صحيحة — لازم يكون مفعّل دايمًا، شوف تحت)، `Common/CurrentUserService` (بيقرا الـ claims من الـ JWT)، `Common/SuperAdminAuthorizationHandler`.

### نمط الوصول للبيانات — غير متسق عبر المشروع

فيه نمطين مستخدمين جنب بعض:
1. **النمط الأصلي (معظم المشروع)**: `IGenericRepository<T>` عام (`GetByIdAsync`, `FindAsync`, `AddAsync`, `Update`, `Remove`) + `IUnitOfWork.SaveChangesAsync()`، والـ Service بيبني الـ DTO يدويًا (مفيش AutoMapper). أمثلة: `FinancialYearService`, `SubProjectService`, `ExecutiveAgencyService`.
2. **نمط الـ Plan/Program** (Marwa): repository مخصص (`IPlanRepo`, `IProgramRepo`) بميثودز خاصة به + AutoMapper (`PlansAndPrograms` profile) لتحويل الكيان لـ DTO. الـ `PlanService`/`PlansController` بيرجعوا أحيانًا الكيان الخام (زي `AddPlan` اللي بيرجع `Plan` مباشرة مش DTO).

لو بتضيف حاجة جديدة في منطقة الـ Plan/Program اتبع نمط AutoMapper الموجود هناك، وأي حاجة تانية اتبع النمط العام. ميرجعش تحاول توحّد النمطين من غير طلب صريح — كل واحد شغال وله استخدامين مختلفين.

## نموذج البيانات

**التسلسل الهرمي للمشروعات**: `MainProgram` → `SubProgram` → `MainProject` → `SubProject`. كل `SubProject` مرتبط بـ `Markaz` (مركز) و`ProjectPriority` و`ProjectStatus`، وممكن يبقى له `ProjectSpecification` (مواصفات فنية، اسم/قيمة/وحدة) و`ExecutiveAgencyId` اختياري.

**السنوات المالية**: `FinancialYear` (اسم، بداية، نهاية، `IsClosed`، `Budget` اختياري) ↔ `SubProject` عبر جدول ربط `SubProjectFinancialYear` (many-to-many — مشروع فرعي ممكن يمتد لأكتر من سنة مالية).

**الخطط (الأرشيف)**: `Plan` (اسم، `StartDate`/`EndDate`، `PlanStatus` enum: `Suggested`/`Approved`، `SuggestionDate`، `ApprovalDate` nullable، مرتبطة بـ `FinancialYear`) ↔ `SubProject` عبر `PlanProject`. الخطة أرشيف فقط — بتتنشئ من زرار طباعة في صفحة المشروعات، مش لها صفحة إدارة مستقلة.

**تعيين المقاولين**: `ExecutiveAgency` و`Contractor` كيانات مستقلة، كل واحد مرتبط بحساب دخول (`ApplicationUser`) بعلاقة واحد-لواحد. `ProjectAssignment` بيربط `SubProject` بـ `Contractor` (اختياري) عبر `ContractType`، وله `IsLocked` (بيتقفل تلقائيًا لو اتغيّرت الجهة التنفيذية للمشروع الفرعي — مقفول لأي حد غير مدير التخطيط/السوبر أدمن). `ProjectAssignmentChangeRequest` لطلبات تعديل تعيين قائم.

**المتابعة**: `ProjectFollowUp` (تقدّم/نسبة مصروف مالي) مرتبطة بـ `SubProjectFinancialYear` — الـ backend جاهز، **مفيش واجهة أمامية لها لسه** (خارج نطاق المرحلة الحالية).

**كيانات موجودة بس مش متفعّلة**: `Notification` — الكيان موجود بس مفيش `DbSet` ليه في `AppDbContext`، ومفيش Controller/Service — كيان يتيم، أيقونة الجرس 🔔 في الواجهة شكلية بس دلوقتي.

## الأدوار والصلاحيات

خمس أدوار في `Roles.cs`:

| الدور | الوصف |
|---|---|
| `SuperAdmin` | صلاحيات كاملة على كل شيء (شوف الآلية تحت) |
| `PlanningManager` | مدير التخطيط — يعتمد الخطط، ينشئ حسابات (موظف تخطيط/جهة تنفيذية/مقاول) |
| `PlanningEmployee` | موظف تخطيط — عمليات CRUD العادية |
| `ExecutiveAgency` | جهة تنفيذية — حساب واحد لكل جهة، يقدر ينشئ حساب مقاول |
| `Contractor` | مقاول — حساب واحد لكل مقاول |

### آلية السوبر أدمن

حساب `SuperAdmin` منفصل تمامًا (`superadmin` / `SuperAdmin@123`، مختلف عن `admin@gmail.com` اللي دوره `PlanningManager`). `SuperAdminAuthorizationHandler` (مسجّل في `Program.cs`) بيخلي أي مستخدم بدور `SuperAdmin` يعدّي **أي** فحص `[Authorize(Roles=...)]` في المشروع تلقائيًا — من غير الحاجة لإضافة `SuperAdmin` يدويًا لكل Controller/Action.

### تحذير مهم: تركيب [Authorize] على مستوى الكلاس والميثود

`[Authorize(Roles=X)]` على مستوى الـ Controller و`[Authorize(Roles=Y)]` على مستوى الـ Action **بيتقاطعوا (AND) مش بيتحدوا (OR)**. يعني لو الكلاس `[Authorize(Roles=Roles.PlanningStaff)]` والميثود `[Authorize(Roles=Roles.PlanningManager)]`، النتيجة الفعلية هي `PlanningManager` بس — الميثود بيضيّق الدائرة، مش بيوسّعها. الاتفاقية المتبعة في المشروع: **الكلاس دايمًا `[Authorize]` بسيط من غير أدوار، وكل Action له سطر الأدوار الخاص بيه صراحة**. اتكشفت المشكلة دي أكتر من مرة أثناء البناء — راجع أي Controller قبل ما تضيف/تعدّل صلاحية.

### مين يقدر ينشئ مين

- `PlanningManager` أو `SuperAdmin`: ينشئ حساب `PlanningEmployee`. إنشاء حساب `PlanningManager` تاني **للسوبر أدمن بس**.
- `PlanningManager` أو `ExecutiveAgency`: ينشئ حساب `Contractor` (`ContractorsController.Create`، `Roles.ManagerAndAgency`).
- `PlanningManager`: ينشئ حساب `ExecutiveAgency`.
- إنشاء حساب `ExecutiveAgency`/`Contractor` بيحصل **مع** إنشاء الكيان بتاعه في نفس الطلب (مفيش مسار لإنشاء حساب منفصل أو ربطه بكيان موجود قبل كده). حذف الكيان بيحذف حسابه تلقائيًا.
- مفيش تسجيل ذاتي (self-registration) خالص — كل حساب بيتعمل يدويًا من حد عنده صلاحية. ده مقصود (نظام داخلي مُدار، مش على الإنترنت العام).

## واجهات الـ API (Controllers)

| Controller | المسؤولية |
|---|---|
| `AuthController` | تسجيل الدخول، تغيير كلمة المرور (مفيش Register) |
| `UsersController` | CRUD حسابات `PlanningEmployee`/`PlanningManager` |
| `MainProjectsController`, `SubProjectsController` | CRUD المشروعات، `SubProjectsController.Search` بيدعم فلتر `financialYearId` |
| `SubProjectFinancialYearsController` | ربط/فك ربط مشروع فرعي بسنة مالية (`api/subprojects/{id}/financial-years`) |
| `FinancialYearsController` | CRUD السنوات المالية |
| `PlansController` | CRUD الخطط + `GET Current` + إضافة مشروع جديد/موجود للخطة + الاعتماد |
| `ProgramController` | قراءة البرامج الرئيسية/الفرعية |
| `LookupsController` | قوائم مرجعية (أولويات، حالات، مراكز، قرى، محافظات) |
| `ExecutiveAgenciesController`, `ContractorsController`, `ContractTypesController` | CRUD الجهات/المقاولين/أنواع العقود |
| `ProjectAssignmentsController` | تعيين مقاول لمشروع فرعي + طلبات التعديل |
| `ProjectSpecificationsController` | المواصفات الفنية للمشروع الفرعي |
| `AuditLogsController` | سجل التعديلات |

## الفرونت إند

### التوجيه (Routing)

- `/` — الشاشة المدمجة (هوية المحافظة + تسجيل الدخول في شاشة واحدة). `/login` بتحوّل تلقائيًا لـ `/`.
- `/app/*` محمي بـ `authGuard`. `/app/dashboard` و`/app/users` محميين إضافيًا بـ `roleGuard([Roles.PlanningManager])` — **ملحوظة**: الـ `SuperAdmin` مش موجود في القائمة دي على مستوى الـ route guard، بس `AuthService.isManager` بيرجّع `true` للسوبر أدمن فبيقدر يشوف زرارير الواجهة المرتبطة — لو محتاج تتأكد إن السوبر أدمن يقدر يوصل لصفحة معينة راجع الـ guard مش بس الزرار.
- `/app/plans/:id` — صفحة طباعة خطة (مفيش صفحة أرشيف/قائمة لكل الخطط، توصلها بس من زرار الطباعة).

### صفحات موجودة

`home` (الشاشة المدمجة)، `dashboard`، `projects` (+ `projects/:id` تفاصيل)، `users`، `plans/:id` (طباعة). **مفيش لسه واجهة مخصصة لأدوار `ExecutiveAgency`/`Contractor`** — الأدوار دي بتقدر تسجّل دخول وتستخدم الـ API مباشرة، بس مفيش صفحات مبنية لها في هذا الفرونت إند.

### اتفاقيات

- CSS عامة مشتركة في `Frontend/src/styles.css` (`si-btn`, `si-modal`, `si-grid`, `si-fld`, إلخ) — استخدمها بدل ما تعيد تعريف حاجة شبهها.
- `AuthService.isManager` = `PlanningManager` أو `SuperAdmin`. استخدمها للـ gating في الواجهة، مش فحص `role() === 'PlanningManager'` مباشرة.
- ميزانية حجم CSS لكل Component: تحذير عند 4kB، خطأ عند 12kB (`angular.json` → `anyComponentStyle`) — اتزوّدت من 8kB لصفحة `home` لأنها فعليًا محتاجة مساحة أكبر (أنيميشن + شعار SVG كامل).

## حسابات البيئة (Seeded)

| الحساب | Username | Password | الدور |
|---|---|---|---|
| السوبر أدمن | `superadmin` | `SuperAdmin@123` | `SuperAdmin` |
| الأدمن الافتراضي | `admin` (`admin@gmail.com`) | `Admin@123` | `PlanningManager` |

بيتزرعوا تلقائيًا عند تشغيل الـ API لو مش موجودين (`Program.cs`).

## التشغيل محليًا

- **الباك إند**: `dotnet run --launch-profile https` من `Backend/src/SmartInvest.API` (الملف الفرونت إند بيتوقع HTTPS على المنفذ 7250 — البروفايل الافتراضي `http` بيشغّل على 5187 بس، ده هيكسر أي طلب من الفرونت إند).
- **قاعدة البيانات**: `Server=.;Database=SmartInvestDB` (`appsettings.json`). لازم SQL Server محلي شغال.
- **Migrations**: لازم تتنفذ من `Backend/src/SmartInvest.API` مع `--project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .` (اختلاف نسخة أداة `dotnet-ef` عن الـ SDK).
- **الفرونت إند**: `npm install` ثم `npx ng serve` من `Frontend`.

## حالة الفروع الحالية (مهم)

- **`main`**: آخر حالة مدفوعة ومتفق عليها من الفريق (مروة + المستخدم). ناقصة: السوبر أدمن، دمج شاشة الدخول/الرئيسية، وتوحيد منطق الـ Plan اللي حصل في `feature/plan-mine-final`.
- **`feature/plan-mine-final`**: أحدث فرع شغال، فيه كل حاجة (اعتماد الخطة بتاريخ يحدده المستخدم + ميثودز مروة لباقي عمليات الخطة + السوبر أدمن + الصلاحيات الجديدة + الشاشة المدمجة). لسه ما اتدمجش في `main` — محتاج مراجعة/اتفاق قبل الدمج.
- **`feature/financial-year-frontend`**: فرع سابق، جزء منه اتدمج في `feature/plan-mine-final`. مش محتاج تشتغل عليه مباشرة.
- **`backup/financial-year-frontend-pre-rebase`**: نسخة احتياطية من قبل عملية Rebase على شغل مروة — للرجوع ليها بس لو احتجنا نقارن.

لو بتبدأ شغل جديد، تأكد إنك على `feature/plan-mine-final` (أو الفرع اللي هيتحدد بعد الدمج مع main) مش `main`.

## مشاكل معروفة / نواقص

- الـ Migration history اتعمله دمج/rebase أكتر من مرة أثناء التعارض مع فرع مروة — لو حصل أي خطأ غريب في migration مستقبلًا، تأكد بـ probe migration فاضية (`dotnet ef migrations add ProbeX` لازم تطلع `Up()`/`Down()` فاضيين) قبل ما تفترض إن الـ snapshot سليم.
- DTOs منطقة الـ Plan (`PlanInfoDto`, `PlanWithoutProjectsDto`) ملهاش `PlanId`, `FinancialYearId`/الاسم، أو `SuggestionDate` — صفحة الطباعة بتعوّض بعرض `StartDate`–`EndDate` بدل اسم السنة المالية، وبدون تاريخ الاقتراح.
- أزرار الطباعة في صفحة المشروعات بتجيب المشروعات الفرعية بـ `pageSize: 5000` (مش pagination حقيقي غير محدود) — كافي للحجم الحالي بس مش حل نهائي لو السنة المالية فيها أكتر من كده.
- `Notification` كيان يتيم (شوف فوق).
- مفيش مكتبة PDF حقيقية — الطباعة بتعتمد على `window.print()` من المتصفح.
