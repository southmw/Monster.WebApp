# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 개발 명령어

```bash
# 솔루션 빌드
dotnet build Monster.WebApp.slnx

# 개발 서버 실행
dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile http

# NuGet 패키지 복원
dotnet restore Monster.WebApp.slnx

# 테스트 실행 (전체)
dotnet test Monster.WebApp.slnx

# 단일 테스트 클래스/메서드 실행
dotnet test Monster.WebApp.slnx --filter "FullyQualifiedName~HtmlContentHelperTests"

# EF Core 마이그레이션
dotnet ef migrations add MigrationName --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
```

> 개발 환경에서는 앱 시작 시 마이그레이션이 자동 적용됨(`Program.ApplyMigrationsAsync`). 프로덕션은 위 `database update`를 배포 단계에서 수동 실행.

**개발 서버**: http://localhost:5104 (HTTP), https://localhost:7056 (HTTPS)

**프로세스 관리 (Windows)**:
```powershell
# dotnet 프로세스 확인
Get-Process -Name dotnet -ErrorAction SilentlyContinue

# 모든 dotnet 프로세스 종료
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 아키텍처

### 프로젝트 구조
- **Monster.WebApp**: 서버 프로젝트 - DB 접근, API, 레이아웃, 대부분의 페이지
- **Monster.WebApp.Client**: 클라이언트 프로젝트 - WebAssembly 전용 컴포넌트
- **Monster.WebApp.Tests**: xUnit 테스트 프로젝트 - 순수 로직(예: PasswordValidator, HtmlContentHelper) 단위 테스트

### 렌더링 모드
- **Server 프로젝트**: 서버 리소스(DB, 파일)가 필요한 컴포넌트, `[StreamRendering]`
- **Client 프로젝트**: `@rendermode InteractiveAuto` 또는 `InteractiveWebAssembly` 컴포넌트

### 서비스 레이어
모든 DB 작업은 서비스 클래스(`Services/`)를 통해 수행. `Program.cs`에서 `AddScoped`로 등록.
- **Auth**: `Services/Auth/` - AuthService, RoleService, UserService
- **Board**: `Services/Board/` - CategoryService, CategoryAccessService, PostService, CommentService
- **공통**: `Services/FileUploadService.cs`

**DbContext 이중 등록** (`Program.cs`): `AddDbContextFactory`(주 사용) + 하위 호환용 `AddScoped<ApplicationDbContext>`(팩토리에서 생성). 신규 서비스는 항상 `IDbContextFactory<ApplicationDbContext>`를 주입받아 사용할 것.

**중요**: IDbContextFactory 패턴 사용 (Blazor Server 동시성 문제 해결)
```csharp
// 서비스 생성자 예시
public PostService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    _contextFactory = contextFactory;
}

// 메서드에서 사용
using var context = await _contextFactory.CreateDbContextAsync();
```

## 기술 스택

- **.NET 8.0** - Blazor Server/WebAssembly 하이브리드
- **MudBlazor 7.16.0** - Material Design UI 프레임워크
- **Entity Framework Core 8.0.11** - SQL Server ORM
- **BCrypt.Net-Next 4.0.3** - 비밀번호 해싱
- **Serilog 9.0.0** - 로깅 (Console + File)

## 인증 시스템

- **방식**: ASP.NET Core Cookie Authentication (7일 세션, SlidingExpiration)
- **쿠키 보안**(`Program.cs`): `HttpOnly=true`, `SameSite=Lax`, `SecurePolicy`는 개발=SameAsRequest / 프로덕션=Always
- **역할**: Admin, SubAdmin, User
- **정책**: AdminOnly, SubAdminOrHigher, AuthenticatedUser
- **상수 정의**: 역할/정책 문자열은 [Shared/AppConstants.cs](Monster.WebApp/Monster.WebApp/Shared/AppConstants.cs)에 중앙 정의 (`AppConstants.Roles.*`, `AppConstants.Policies.*`). 하드코딩 금지.
- **수정/삭제 권한 검증**: `AuthService.CanModifyContent(ownerUserId, authorPasswordHash, providedPassword)` 단일 메서드로 통일 (관리자 통과 / 로그인 작성물은 본인 / 익명 작성물은 비밀번호 검증). PostService·CommentService의 Update/Delete가 이를 호출 — 권한 로직을 복제하지 말 것.
- **로그인 보안**: 5회 실패 시 15분 잠금 (IP + 사용자명 조합, MemoryCache 기반)
- **비밀번호 정책**: 최소 8자, 대문자/소문자/숫자/특수문자 각 1개 필수. 검증은 [Shared/PasswordValidator.cs](Monster.WebApp/Monster.WebApp/Shared/PasswordValidator.cs)에서 수행.
- **기본 관리자 시드**: `Program.InitializeDefaultAdminAsync()`가 앱 시작 시 admin 계정이 없으면 자동 생성. 시드 정보는 설정 `AdminSeed:Username` / `AdminSeed:Email` / `AdminSeed:Password`에서 읽음(미지정 시 기본값 fallback, 비밀번호는 로그에 기록하지 않음).
- **접근 거부**: 권한 부족 시 `/account/access-denied` (`Components/Pages/Account/AccessDenied.razor`)로 이동

**초기 관리자**: `AdminSeed:Password` 미설정 시 기본값 `Admin@123!`로 생성 (프로덕션에서는 설정으로 지정하거나 즉시 변경 필요)

**API 엔드포인트** (`Controllers/`, Razor 컴포넌트와 별개의 MVC 컨트롤러):
- `POST /api/auth/login`: 로그인 (`{username, password}`) — 쿠키 발급
- `POST /api/auth/logout` / `GET /api/auth/logout`: 로그아웃
- 파일 업로드: `Controllers/FileUploadController.cs` — `[Authorize]` 필수, 확장자 화이트리스트 + 파일 시그니처(매직넘버) 검증, 본문 한도 50MB. **현재 호출하는 UI가 없는 비활성 코드**(Quill 에디터 제거 잔재)이며 향후 이미지 업로드 재도입 대비로 보존.

## UI 프레임워크 (MudBlazor 7.16.0)

### 핵심 호환성 주의사항

**다이얼로그 Cascading Parameter** (필수):
```csharp
// ✅ 올바른 방식
[CascadingParameter]
private IMudDialogInstance? MudDialog { get; set; }

private void Cancel() => MudDialog?.Cancel();
private void Submit() => MudDialog?.Close(DialogResult.Ok(true));
```

**기타 제한사항**:
- `Typography` 타입 미지원 (커스텀 테마에서 제거)
- `MudSnackbarProvider`의 `Position` 속성 미지원
- `Shadows.Elevation`는 26개 값 필요

### UI 디자인 규칙
- 입력 폼: `Margin="Margin.Dense"`, `MudGrid Spacing="1"`
- 테마: 보라색 그라디언트 (#667eea → #764ba2)
- 커스텀 테마: [Shared/CustomTheme.cs](Monster.WebApp/Monster.WebApp/Shared/CustomTheme.cs)

### 비밀번호 필드 UX
비밀번호 필드에는 표시/숨김 토글 기능 구현:
```razor
<MudTextField @bind-Value="password"
              InputType="@(_showPassword ? InputType.Text : InputType.Password)"
              Adornment="Adornment.End"
              AdornmentIcon="@(_showPassword ? Icons.Material.Filled.VisibilityOff : Icons.Material.Filled.Visibility)"
              OnAdornmentClick="() => _showPassword = !_showPassword" />
```
적용 위치: Login.razor, Register.razor, Profile.razor

## 데이터베이스

- **DB**: SQL Server 2022 with EF Core 8.0.11
- **연결**: `appsettings.json` → `ConnectionStrings:DefaultConnection`
- **비밀번호 해싱**: BCrypt.Net-Next 4.0.3
- **읽기 쿼리**: 조회 전용 메서드는 `.AsNoTracking()` 사용 (PostService/CommentService/CategoryService 목록·상세 조회)

### 데이터 모델
- **Auth**: User, Role, UserRole, CategoryAccess
- **Board**: Category, Post, Comment, Attachment, PostVote

### 삭제 규칙
- Post 삭제 → Comment/Attachment 자동 삭제 (Cascade)
- Category 삭제 → 게시글 존재 시 불가 (Restrict)
- User 삭제 → Post/Comment의 UserId null 설정 (SetNull)

## 게시판 기능

- **조회수 중복 방지**: 세션 기반 (같은 세션에서 재조회 시 카운트 미증가)
- **추천 중복 방지**: `PostVote` 모델로 투표 기록 저장 (로그인 사용자: UserId, 비로그인: IP 주소)
- **공지 고정**: `Post.IsPinned`/`PinnedAt` 필드. `PostService.TogglePinAsync()`로 토글 (Admin/SubAdmin 권한). 목록 정렬은 공지글(PinnedAt 최신순) → 일반글(CreatedAt 최신순), 공지글은 상단에 보라색 배경 + "공지" 칩 표시
- **카테고리 접근 제어**: `CategoryAccess` 모델 + `CategoryAccessService` (N+1 회피 위해 전체 로딩 후 메모리 필터링)
- **검색**: `/board/{slug}` 목록에서 지원
- **관리자 비밀번호 리셋**: 사용자 관리(UserList)에서 재설정 (`Components/Pages/Admin/Users/ResetPasswordDialog.razor`)

## 주요 라우팅

| 경로 | 설명 | 권한 |
|------|------|------|
| `/` | 홈페이지 | 공개 |
| `/board` | 게시판 카테고리 목록 | 공개 |
| `/board/{slug}` | 게시글 목록 (검색 지원) | 공개 |
| `/board/{slug}/{postId}` | 게시글 상세 | 공개 |
| `/board/{slug}/{postId}/edit` | 게시글 수정 | 작성자/비밀번호 또는 관리자 |
| `/account/access-denied` | 접근 거부 | 공개 |
| `/board/{slug}/write` | 게시글 작성 | 공개 |
| `/account/login` | 로그인 | 공개 |
| `/account/register` | 회원가입 | 공개 |
| `/profile` | 내 프로필 | 로그인 |
| `/admin` | 관리자 대시보드 | Admin |
| `/admin/users` | 사용자 관리 | Admin |
| `/admin/categories` | 카테고리 관리 | Admin |
| `/admin/settings` | 설정 (관리 페이지 바로가기) | Admin |

## 로깅

Serilog를 사용하여 콘솔 및 파일 로깅 (`Program.cs`):
- **콘솔**: 실시간 로그 출력
- **파일**: `logs/log-{날짜}.txt` (일별 롤링, 최근 31일치 보관)
- **최소 레벨**: 기본 Information, `Microsoft`/`Microsoft.AspNetCore`/`Microsoft.EntityFrameworkCore`는 Warning으로 상향 (EF Core SQL/파라미터가 로그에 남지 않도록 — 민감정보 노출 방지)

## 주요 컴포넌트 및 파일

### 에디터 및 파일 업로드
- `Services/FileUploadService.cs` / `Controllers/FileUploadController.cs` - 이미지/동영상 업로드 로직
- 파일 저장 경로: `wwwroot/uploads/posts/{postId}/` (런타임 생성물 — `.gitignore` 처리됨, `.gitkeep`으로 폴더만 보존)
- 지원 형식: 이미지(jpg, jpeg, png, gif, webp - 최대 10MB), 동영상(mp4, webm - 최대 50MB)
- **현재 비활성**: 본문 에디터가 평문이라 업로드를 호출하는 UI가 없음. 코드는 향후 재도입 대비로 보존(인증·매직넘버 검증 적용됨).

### HTML 렌더링 (XSS 방어)
게시글/댓글 본문은 평문 입력이므로, 출력 시 [Shared/HtmlContentHelper.cs](Monster.WebApp/Monster.WebApp/Shared/HtmlContentHelper.cs)의 `ToSafeHtml()`로 변환한 뒤 `@((MarkupString)...)`로 렌더링한다 (모든 HTML 특수문자 인코딩 + 줄바꿈→`<br>`). **사용자 입력을 정제 없이 `MarkupString`으로 직접 출력하지 말 것** — 저장형 XSS 위험.

## 에디터

- **방식**: MudTextField (Lines="15") 사용하는 일반 텍스트 입력
- **WYSIWYG 에디터**: Blazored.TextEditor (Quill.js)는 호환성 문제로 제거됨
