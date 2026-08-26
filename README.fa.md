<div dir="rtl">

# BaseRepository

[![CI](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/actions/workflows/ci.yml/badge.svg)](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/actions/workflows/ci.yml)

یک قالب آماده (starter template) با معماری Clean Architecture برای APIهای
دات‌نت ۱۰. هر چیزی که بیشتر پروژه‌ها از روز اول لازم دارند — لایه‌بندی، CRUD
جنریک، احراز هویت، صفحه‌بندی، اعتبارسنجی، مستندات OpenAPI و لاگ ساخت‌یافته —
از قبل نوشته و وصل شده است.

📄 **[English version of this guide](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/blob/master/README.md)**

---

## شروع سریع

**به SDK دات‌نت ۱۰ نیاز دارد** (`dotnet --version` باید با `10.` شروع شود). روی
SDK قدیمی‌تر، قالب از همان اول از ساختن پروژه خودداری می‌کند تا پروژه‌ای که
restore نمی‌شود روی دستتان نماند — اگر خطای
`NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` را
دیدید، دلیلش همین است. از
[dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
نصبش کنید.

قالب را از NuGet نصب کنید، یک پروژه با نام دلخواه خودتان بسازید و اجرا کنید:

<div dir="ltr">

```bash
dotnet new install BaseRepository.Template::1.0.0-preview.1
dotnet new basecrud -n Acme.Store
cd Acme.Store
dotnet run --project src/Api
```

</div>

<div dir="ltr">

[![NuGet](https://img.shields.io/nuget/vpre/BaseRepository.Template.svg)](https://www.nuget.org/packages/BaseRepository.Template)

</div>

این پکیج فعلاً prerelease است، برای همین شمارهٔ نسخه صریح نوشته شده — اگر
`dotnet new install BaseRepository.Template` را بدون نسخه بزنید فقط نسخه‌های
پایدار را پیدا می‌کند. می‌توانید از روی کلون محلی یا یک `.nupkg` که خودتان
ساخته‌اید هم نصب کنید:

<div dir="ltr">

```bash
dotnet new install <path-to-this-repo-or-its-.nupkg>
```

</div>

همه چیز — namespaceها، فایل‌های پروژه و خود solution — به نامی که با `-n`
می‌دهید تغییر نام پیدا می‌کند. نیازی به search & replace دستی نیست.

> **اسمی انتخاب کنید که شناسهٔ معتبر C# باشد.** نقطه مشکلی ندارد
> (`Acme.Store`، `Contoso.Widgets.Api`) و رقم هم بعد از کاراکتر اول مجاز است.
> اسمی که خط تیره داشته باشد (`my-api`) ساخته می‌شود ولی **build نمی‌شود**:
> فایل solution به `my_api.*.csproj` ارجاع می‌دهد در حالی که فایل‌های پروژه روی
> دیسک با اسم خام نام‌گذاری شده‌اند، و `dotnet build` با خطای MSB3202 شکست
> می‌خورد. به‌جایش از `MyApi` یا `My.Api` استفاده کنید.

می‌توانید همین مخزن را هم مستقیم clone و اجرا کنید:

<div dir="ltr">

```bash
dotnet run --project src/Api
```

</div>

### چیزهایی که بلافاصله در اختیار دارید

| مسیر | توضیح |
| --- | --- |
| `GET /health` | بررسی سلامت سرویس |
| `GET /` | وضعیت سرویس |
| `GET /openapi/v1.json` | سند OpenAPI |
| `GET /scalar/v1` | مستندات تعاملی API (Scalar) |

این‌ها بدون هیچ تنظیماتی کار می‌کنند. یک دیتابیس SQLite (فایل `app.db`) هم در
اولین اجرا به‌صورت خودکار ساخته می‌شود.

---

## تنظیمات

فقط یک تنظیم قبل از استفاده از endpointهای محافظت‌شده لازم است: کلید امضای JWT.
این مقدار عمداً در `appsettings.json` خالی گذاشته شده — هیچ‌وقت یک secret
پیش‌فرض قابل استفاده نباید همراه پروژه منتشر شود.

<div dir="ltr">

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets set Jwt:SigningKey "<at least 32 bytes>" --project src/Api
```

</div>

| تنظیم | کاربرد |
| --- | --- |
| `Jwt:SigningKey` | کلید امضا و اعتبارسنجی توکن‌ها (**الزامی**) |
| `Jwt:Issuer` | صادرکنندهٔ توکن |
| `Jwt:Audience` | مخاطب توکن |
| `ConnectionStrings:Default` | رشتهٔ اتصال به دیتابیس |

اگر ترجیح می‌دهید، به‌جای user-secrets می‌توانید از متغیرهای محیطی
(environment variables) استفاده کنید.

---

## احراز هویت

ثبت‌نام و ورود بخشی از خود قالب هستند — نمونهٔ آزمایشی نیستند که بعداً حذفشان
کنید.

**ثبت‌نام** — `POST /api/v1/auth/register` ← **۲۰۱ Created**

<div dir="ltr">

```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"me@example.com","password":"correct-horse-battery"}'
# => { "userId": 1, "email": "me@example.com", "token": "...", "expiresAt": "..." }
```

</div>

**ورود** — `POST /api/v1/auth/login` ← **۲۰۰ OK** (همان بدنه، همان ساختار پاسخ).

سپس توکن را روی هر درخواست محافظت‌شده بفرستید:

<div dir="ltr">

```bash
curl http://localhost:5000/api/v1/todo-items \
  -H "Authorization: Bearer <token>"
```

</div>

نکته‌هایی که خوب است بدانید:

- رمزهای عبور با BCrypt هش می‌شوند و هرگز به‌صورت متن ساده ذخیره یا مقایسه
  نمی‌شوند.
- **فقط ایمیل شناسهٔ ورود است.** شمارهٔ موبایل هیچ‌وقت جایگزین آن نیست.
- ثبت‌نام با ایمیلی که قبلاً استفاده شده، خطای **۴۰۹** برمی‌گرداند.
- رمز اشتباه و ایمیل ناموجود، هر دو یک خطای **۴۰۱** یکسان می‌دهند؛ بنابراین
  پاسخ فاش نمی‌کند که یک ایمیل ثبت شده است یا نه.

### مدیریت شمارهٔ موبایل

`PUT /api/v1/auth/me/phone` — به توکن نیاز دارد. کاربری که ویرایش می‌شود از
داخل توکن خوانده می‌شود، پس کسی نمی‌تواند پروفایل دیگری را تغییر دهد.

<div dir="ltr">

```bash
curl -X PUT http://localhost:5000/api/v1/auth/me/phone \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"09123456789","region":"IR"}'
# => { "userId": 1, "email": "me@example.com", "phoneNumber": "+989123456789" }
```

</div>

شماره‌های **همهٔ کشورها** پشتیبانی می‌شوند و به فرم E.164 ذخیره می‌شوند
(`+989123456789`، `+16502530000`، …). یا شماره را در قالب بین‌المللی بفرستید و
`region` را ندهید، یا شماره را در قالب محلی همراه با کد کشور به فرم
ISO 3166-1 alpha-2 بفرستید (`"IR"`، `"US"`، `"GB"`، …). شمارهٔ محلی بدون
`region` به‌جای حدس زدن، به‌عنوان مبهم رد می‌شود. برای پاک کردن شماره مقدار
`null` بفرستید؛ شماره‌ای که کاربر دیگری استفاده کرده باشد خطای **۴۰۹** می‌دهد.

---

## اضافه کردن موجودیت (entity) خودتان

این اصلی‌ترین کاری است که قالب برایش ساخته شده. سه قدم کوتاه:

**۱. موجودیت** (لایهٔ Domain):

<div dir="ltr">

```csharp
public class Product : BaseEntity<int>, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    // IAuditableEntity gives you automatic CreatedAt / LastModifiedAt
}
```

</div>

**۲. یک DTO و ثبت آن** (لایهٔ Application). اگر می‌خواهید پاسخ `POST` هدر
`Location` درست داشته باشد، `IHasId<TKey>` را روی DTO پیاده‌سازی کنید:

<div dir="ltr">

```csharp
public record ProductDto(int Id, string Name, decimal Price) : IHasId<int>;

// in your DI setup:
services.AddCrudHandlers<Product, int, ProductDto>();
```

</div>

**۳. یک کنترلر** (لایهٔ Api):

<div dir="ltr">

```csharp
[Route("api/v{version:apiVersion}/products")]
public class ProductsController : BaseCrudController<Product, int, ProductDto>
{
    public ProductsController(ISender sender) : base(sender) { }
}
```

</div>

همین — حالا یک CRUD کامل REST دارید:

| متد | مسیر | توضیح |
| --- | --- | --- |
| `GET` | `/api/v1/products?pageIndex=1&pageSize=20` | لیست صفحه‌بندی‌شده |
| `GET` | `/api/v1/products/{id}` | یک آیتم |
| `POST` | `/api/v1/products` | ایجاد |
| `PUT` | `/api/v1/products/{id}` | ویرایش |
| `DELETE` | `/api/v1/products/{id}` | حذف |

همهٔ این‌ها به‌صورت پیش‌فرض به توکن نیاز دارند. برای باز کردن دسترسی، روی
کنترلر خودتان `[AllowAnonymous]` بگذارید.

### اعتبارسنجی (اختیاری)

کافی است یک `AbstractValidator<T>` هر جای پروژهٔ Application بگذارید — خودکار
پیدا و ثبت می‌شود:

<div dir="ltr">

```csharp
public class CreateProductValidator : AbstractValidator<CreateCommand<Product, int, ProductDto>>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Price).GreaterThan(0);
    }
}
```

</div>

موجودیتی که validator نداشته باشد، ساده از اعتبارسنجی رد می‌شود. خطاها به شکل
**۴۰۰** همراه با یک آبجکت `errors` به تفکیک فیلد برمی‌گردند.

### کوئری‌های سفارشی

برای هر چیزی فراتر از CRUD، از `Specification<T>` ارث‌بری کنید تا فیلتر/
include/مرتب‌سازی/صفحه‌بندی را بدون درگیر شدن با EF Core توصیف کنید و بعد آن را
به `IRepository<TEntity, TKey>` بدهید. برای منطق کسب‌وکار واقعی، یک جفت
`IRequest`/`IRequestHandler` اختصاصی بنویسید — مسیر جنریک پیش‌فرض است، نه اجبار.

---

## موجودیت نمونه

`TodoItem` (موجودیت، DTO، validator، کنترلر و `DbSet`) یک نمونهٔ واقعی و کارا
است که به اپلیکیشن در حال اجرا وصل شده تا کل الگو را از ابتدا تا انتها ببینید.
تنها چیزی در این مخزن است که قرار است حذف شود — به‌محض اینکه موجودیت‌های خودتان
را اضافه کردید، پاکش کنید. روی تک‌تک این تایپ‌ها یک doc comment هست که همین را
یادآوری می‌کند.

---

## امکانات

**معماری**

- چهار لایه: `Domain` ← `Application` ← `Infrastructure` ← `Api`، با وابستگی‌هایی
  که فقط رو به داخل هستند. `Domain` به هیچ چیزی وابسته نیست.
- `Result` و `Result<T>` برای یک شکل یکنواخت موفقیت/شکست، و `PagedResult<T>`
  برای متادیتای صفحه (`TotalPages`، `HasNextPage`، `HasPreviousPage`).
- استثناهای دامنه (`NotFoundException`، `ConflictException`،
  `BusinessRuleException`، `AuthenticationFailedException`) که لایهٔ API خودکار
  به کدهای وضعیت HTTP ترجمه می‌کند.

**دسترسی به داده**

- `IRepository<TEntity, TKey>` جنریک با پیاده‌سازی EF Core که با هر `DbContext`
  کار می‌کند، به‌همراه `IUnitOfWork` برای ذخیرهٔ صریح تغییرات و
  `IUnitOfWork.BeginTransactionAsync()` برای وقتی چند نوشتن — یا یک نوشتن و
  کاری که بعدش می‌آید — باید با هم موفق یا با هم ناموفق شوند. commit را صریح
  انجام دهید؛ dispose شدن یک transaction ای که commit نشده آن را rollback
  می‌کند، پس `await using` انتخاب امن پیش‌فرض است.
- الگوی Specification برای ساختن کوئری‌ها بدون نشت EF Core به کد لایهٔ
  Application.
- `IAuditableEntity` (زمان‌های خودکار ایجاد/ویرایش) و `ISoftDelete` (حذف تبدیل
  به به‌روزرسانی می‌شود و رکوردهای حذف‌شده به‌صورت سراسری فیلتر می‌شوند) —
  هر دو اختیاری و انتخابی.

**API**

- مدیریت سراسری خطا: هر خطا به شکل `ProblemDetails` مطابق RFC 7807 برمی‌گردد —
  کدهای ۴۰۴، ۴۰۹، ۴۰۱، ۴۲۲، ۴۰۰ و ۵۰۰ از روی استثناهای دامنه نگاشت می‌شوند.
- نسخه‌بندی API (`api/v{version:apiVersion}/…`) با یک سند OpenAPI جدا برای هر
  نسخه.
- Output caching وصل شده اما عمداً روی کنترلرهای CRUD **اعمال نشده** است: کش
  کردن پاسخ یک endpoint محافظت‌شده بدون در نظر گرفتن کاربر در کلید کش، یعنی
  نشت دادهٔ یک کاربر به کاربر دیگر. خودتان `[OutputCache]` را جایی بگذارید که
  دربارهٔ آن فکر کرده‌اید.
- لاگ ساخت‌یافته با Serilog — به‌ازای هر درخواست یک خط خلاصه.

**لایسنس وابستگی‌ها**

تمام وابستگی‌ها لایسنس آزاد و بی‌دردسر دارند (MIT / Apache-2.0). از MediatR و
AutoMapper عمداً استفاده **نشده** است — لایسنس نسخهٔ ۱۳ به بعدشان می‌تواند یک
پروژهٔ کلوزسورس را مجبور کند یا سورسش را باز کند یا لایسنس بخرد. به‌جای آن‌ها،
یک mediator کوچک داخلی با همان شکل (`IRequest<T>`،
`IRequestHandler<T,TResponse>`، `ISender`، `IPipelineBehavior<T,TResponse>`) و
Mapster برای mapping استفاده شده. هیچ‌چیزی در این قالب تعهد لایسنسی روی پروژهٔ
شما نمی‌گذارد.

---

## ابزارهای بومی‌سازی ایران

چند helper کوچک، بدون وابستگی خارجی و کاملاً اختیاری در
`BaseRepository.Domain.Common`. به‌صورت پیش‌فرض به هیچ جایی وصل نیستند — هرکدام
را که لازم داشتید استفاده کنید. (برای شمارهٔ موبایل کشورهای دیگر، از
`IPhoneNumberValidator` که در بخش احراز هویت توضیح داده شد استفاده کنید؛ این‌ها
عمداً فقط مخصوص ایران هستند.)

- **`IranianCalendar`** — متدهای `ToShamsi` / `FromShamsi` / `ToHijri` /
  `FromHijri` بین `DateTime` و تاپل `(Year, Month, Day)` شمسی/قمری تبدیل
  می‌کنند. توجه کنید `HijriCalendar` از الگوریتم جدولی ثابت استفاده می‌کند نه
  تقویم اُمّ‌القری، پس ممکن است برای مناسبت‌های مذهبیِ رؤیت‌محور یک روز اختلاف
  داشته باشد.
- **`IranianNationalCode.IsValid(string)`** — رقم کنترلی کد ملی را اعتبارسنجی
  می‌کند و رشته‌هایی که همهٔ ارقامشان یکسان است را رد می‌کند (این‌ها ذاتاً از
  چک‌سام رد می‌شوند ولی هیچ‌وقت کد ملی واقعی نیستند).
- **`PersianMobileNumber.IsValid` / `.Normalize`** — پیشوندهای رایج `+98`،
  `0098`، `98` و `0` را می‌پذیرد و همه را به فرم `09XXXXXXXXX` نرمال می‌کند.

دو مورد آخر به‌شکل قاعدهٔ FluentValidation هم در دسترس هستند:

<div dir="ltr">

```csharp
RuleFor(x => x.Phone).PersianMobileNumber();
RuleFor(x => x.NationalCode).IranianNationalCode();
```

</div>

---

## اجرای تست‌ها

<div dir="ltr">

```bash
dotnet test
```

</div>

هر لایه پروژهٔ تست خودش را دارد — تست واحد برای Domain و Application، تست
یکپارچگی روی یک دیتابیس SQLite واقعی برای Infrastructure، و تست عملکردی برای
API که همان pipeline واقعی `Program.cs` را روی HTTP واقعی صدا می‌زند.

---

## ساختار solution

<div dir="ltr">

```
src/
  Domain/           zero dependencies
  Application/      depends on Domain
  Infrastructure/   depends on Application
  Api/              depends on Application + Infrastructure
tests/
  *.Domain.UnitTests
  *.Application.UnitTests
  *.Infrastructure.IntegrationTests
  *.Api.FunctionalTests
```

</div>

---

## لایسنس

MIT — فایل [LICENSE](https://github.com/alireza-flhz/CleanArchitecture-Api-Template/blob/master/LICENSE) را ببینید.

</div>
