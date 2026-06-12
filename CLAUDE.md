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

> 솔루션 파일은 `.sln`이 아닌 `.slnx`(신형 XML 솔루션 포맷)임 — 구버전 도구 사용 시 주의.

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
- **Monster.WebApp.Tests**: xUnit 테스트 프로젝트. 위치는 루트가 아닌 `Monster.WebApp/Monster.WebApp.Tests/` (서버 프로젝트 폴더와 나란히 중첩)
  - 순수 로직 테스트: PasswordValidatorTests, HtmlContentHelperTests
  - 서비스 테스트: AuthServiceCanModifyContentTests, CategoryAccessServiceTests — SQLite in-memory 기반 (`TestInfrastructure.cs`의 `TestDbContextFactory`(EnsureCreated로 HasData 시드 포함) + `TestHttpContext` 헬퍼 사용. 새 서비스 테스트도 이 인프라를 재사용할 것)

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
- **Spillgebees.Blazor.RichTextEditor 2.2.0** - WYSIWYG 에디터 (Quill 2 기반, MIT)
- **HtmlSanitizer 9.0.892** - 리치 에디터 HTML 새니타이징 (Ganss.Xss)
- **Entity Framework Core 8.0.11** - SQL Server ORM
- **BCrypt.Net-Next 4.0.3** - 비밀번호 해싱
- **Serilog 9.0.0** - 로깅 (Console + File)

## 인증 시스템

- **방식**: ASP.NET Core Cookie Authentication (7일 세션, SlidingExpiration)
- **쿠키 보안**(`Program.cs`): `HttpOnly=true`, `SameSite=Lax`, `SecurePolicy`는 개발=SameAsRequest / 프로덕션=Always. 단, **HTTPS 미지원 호스팅**(현재 MonsterASP 무료 플랜)에서는 Secure 쿠키를 브라우저가 저장하지 않아 로그인이 유지되지 않음 → 설정 `Auth:AllowInsecureHttpCookies=true`(appsettings.json)로 프로덕션에서도 SameAsRequest 허용. **HTTPS 가능한 환경으로 이전 시 이 설정을 반드시 제거할 것**(인증 쿠키 평문 전송 위험)
- **쿠키 사용자 검증**(`OnValidatePrincipal`): 쿠키의 사용자 Id가 DB에 실재하고 `IsActive`인지 확인 (사용자별 5분 MemoryCache — 키는 `AppConstants.CacheKeys.UserValidPrefix`). 불일치 시 자동 로그아웃 — 스테일 쿠키 FK 오류와 비활성 사용자 세션 잔존 방지. **유효(true) 결과만 캐시할 것** — 무효 결과를 캐시하면 같은 Id 재로그인(DB 재생성 후 재가입 등) 시 로그인이 즉시 풀리는 루프 발생. 로그인 성공 시 캐시 갱신(AuthService), 비활성화 시 캐시 제거(UserService)로 즉시 반영
- **역할**: Admin, SubAdmin, User
- **정책**: AdminOnly, SubAdminOrHigher, AuthenticatedUser
- **상수 정의**: 역할/정책 문자열은 [Shared/AppConstants.cs](Monster.WebApp/Monster.WebApp/Shared/AppConstants.cs)에 중앙 정의 (`AppConstants.Roles.*`, `AppConstants.Policies.*`). 하드코딩 금지.
- **수정/삭제 권한 검증**: `AuthService.CanModifyContent(ownerUserId, authorPasswordHash, providedPassword)` 단일 메서드로 통일 (관리자 통과 / 로그인 작성물은 본인 / 익명 작성물은 비밀번호 검증). PostService·CommentService의 Update/Delete가 이를 호출 — 권한 로직을 복제하지 말 것.
- **로그인 보안**: 5회 실패 시 15분 잠금 (IP + 사용자명 조합, MemoryCache 기반)
- **회원가입 제한**: IP당 1시간에 3회 (MemoryCache 기반). HttpContext 없는 내부 호출(관리자 시드)은 제한 제외
- **클라이언트 IP 추출**: [Shared/ClientIpHelper.cs](Monster.WebApp/Monster.WebApp/Shared/ClientIpHelper.cs)의 `GetClientIp()`만 사용 — `RemoteIpAddress` 기반. **X-Forwarded-For 헤더를 직접 파싱하지 말 것**(스푸핑으로 잠금/중복투표 우회 가능). 프록시 뒤 배포 시 설정 `ForwardedHeaders:KnownProxies`(string[])에 프록시 IP를 지정하면 `Program.cs`의 ForwardedHeaders 미들웨어가 RemoteIpAddress를 재작성함
- **응답 보안 헤더**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` — `Program.cs` 인라인 미들웨어. CSP는 Blazor Server + MudBlazor의 인라인 스크립트/스타일 의존성 때문에 미도입(도입 시 별도 검토 필요)
- **비밀번호 정책**: 최소 8자, 대문자/소문자/숫자/특수문자 각 1개 필수. 검증은 [Shared/PasswordValidator.cs](Monster.WebApp/Monster.WebApp/Shared/PasswordValidator.cs)에서 수행.
- **기본 관리자 시드**: `Program.InitializeDefaultAdminAsync()`가 앱 시작 시 admin 계정이 없으면 자동 생성. 시드 정보는 설정 `AdminSeed:Username` / `AdminSeed:Email` / `AdminSeed:Password`에서 읽음(미지정 시 기본값 fallback, 비밀번호는 로그에 기록하지 않음).
- **접근 거부**: 권한 부족 시 `/account/access-denied` (`Components/Pages/Account/AccessDenied.razor`)로 이동

**초기 관리자**: `AdminSeed:Password` 미설정 시 기본값 `Admin@123!`로 생성 (프로덕션에서는 설정으로 지정하거나 즉시 변경 필요)

**API 엔드포인트** (`Controllers/`, Razor 컴포넌트와 별개의 MVC 컨트롤러):
- `POST /api/auth/login`: 로그인 (`{username, password}`) — 쿠키 발급
- `POST /api/auth/logout` / `GET /api/auth/logout`: 로그아웃
- 파일 업로드 API: `Controllers/FileUploadController.cs` — `[Authorize]` 필수, 확장자 화이트리스트 + 파일 시그니처(매직넘버) 검증(검증 로직은 `FileUploadService` 정적 메서드 공유), 본문 한도 50MB. 단, 에디터 미디어 업로드는 이 API가 아니라 Blazor 컴포넌트에서 `FileUploadService.UploadFileAsync`를 직접 호출함 — 이 API는 외부/비-Blazor 클라이언트용 보조 경로.

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

### UI 디자인 규칙 (디자인 시스템)
- **방향**: 모던·심플 — Slate 캔버스 + Indigo 단일 액센트. 사이드바는 모드별 톤 추종(라이트=Slate 50+테두리 구분 / 다크=어둡게), 히어로 캔버스(`home-hero`)만 다크 고정 액센트
- **폰트**: Pretendard Variable (App.razor에서 jsdelivr CDN 로드, CustomTheme Typography에 지정)
- **커스텀 테마**: [Shared/CustomTheme.cs](Monster.WebApp/Monster.WebApp/Shared/CustomTheme.cs) — Primary Indigo(#6366F1/#818CF8), LinesDefault/Divider/TableLines 정의됨
- **색상 하드코딩 금지**: CSS는 MudBlazor 팔레트 변수(`var(--mud-palette-*)`)를 사용해 라이트/다크 자동 대응. 소프트 틴트는 `color-mix(in srgb, var(--mud-palette-primary) N%, transparent)`로 파생. `.mud-theme-light`/`.mud-theme-dark` 분기 스타일은 팔레트 변수로 해결 불가할 때만 사용
- **CSS 파일 구성** (wwwroot): `css/layout.css`(앱바·사이드바·푸터·NavMenu), `css/custom.css`(페이지·카드·게시판·댓글·에디터), `css/account.css`(인증 페이지), `app.css`(폼 유효성)
- **CSS 캐시 버스팅**: 로컬 CSS 링크는 App.razor에서 `?v=@AssetVersion`(앱 시작 시각 Ticks) 쿼리 부착 — `UseStaticFiles` 기본값은 Cache-Control 미전송이라 브라우저 휴리스틱 캐시로 CSS 갱신이 전달되지 않는 문제 방지. **새 CSS 파일 추가 시 동일하게 `?v=@AssetVersion`을 붙일 것** (재시작/재배포 시 자동 무효화)
- **공통 컴포넌트/헬퍼**:
  - `Components/Shared/EmptyState.razor` — 빈 목록/에러/접근 거부 상태 표준 UI (Icon/Title/Description/Severity + 액션 버튼 슬롯). 페이지에 상태 UI를 새로 만들지 말고 이것을 사용
  - `Shared/CategoryDisplayHelper.GetIcon(slug)` — 카테고리 아이콘 매핑 중앙화 (NavMenu/Home/Board Index 공용)
  - `Shared/TimeDisplayHelper.ToRelative(utc)` — 상대시간 표시 ("방금 전"/"N분 전"/…, 7일 이상은 MM/dd)
  - 카드 류는 custom.css의 `content-card`/`category-card`/`stat-tile` 클래스 재사용
- **로딩 상태**: 페이지 최초 로딩은 MudSkeleton(실제 레이아웃 골격)으로, 부분 갱신은 MudProgressCircular
- **레이아웃**: MudDrawer는 데스크톱(≥Md)=`Mini`(햄버거로 닫으면 68px 아이콘 레일) / 모바일(<Md)=`Temporary`(오버레이) — MainLayout이 `IBrowserViewportService`(IBrowserViewportObserver 구현)로 브레이크포인트를 구독해 Variant 전환. 푸터는 MainLayout의 `app-footer`. 히어로 캔버스(`home-hero` 클래스)는 홈/프로필 공용
- **홈 피드**: 최근/인기 게시글은 단일 카드 + 커스텀 탭 토글(`home-feed-tabs`, MudTabs 미사용), 행에 카테고리 칩 + 상대시간, 각 8건 표시
- **게시글 목록**: MudTable이 아닌 div 기반 리스트(`post-list`/`post-list-item`) — 모바일 대응. 공지글은 `post-list-item-pinned` + `post-pin-chip`
- 입력 폼: `Margin="Margin.Dense"`, `MudGrid Spacing="1"`
- 인라인 `Style=` 사용 자제 — 색상/굵기는 테마와 CSS 클래스로 해결 (h4~h6 굵기는 테마 Typography에 정의됨)

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
- **주의**: `appsettings.json`은 Git에 추적되지 않음 (DB 자격증명 포함). 새 클론 시 직접 생성 필요 — 형식은 README.md의 "데이터베이스 설정" 참조
- **비밀번호 해싱**: BCrypt.Net-Next 4.0.3
- **읽기 쿼리**: 모든 서비스의 조회 전용 메서드는 `.AsNoTracking()` 사용 (수정 후 SaveChanges 하는 메서드는 제외)

### 데이터 모델
- **Auth**: User, Role, UserRole, CategoryAccess
- **Board**: Category, Post, Comment, Attachment, PostVote
- **댓글 중첩**: `Comment.ParentCommentId`(self-reference) + `Replies` 컬렉션으로 답글 트리 구성
- **본문 형식**: `Post.IsHtml`/`Comment.IsHtml` — true면 새니타이즈된 HTML(리치 에디터), false면 레거시 평문. `Post.SearchText`는 태그 제거된 검색용 본문

### 시드 데이터 (`ApplicationDbContext.OnModelCreating`의 `HasData`)
- 역할 3종(Admin/SubAdmin/User), 기본 카테고리 3개: 자유게시판(`free`), 질문게시판(`questions`), 정보공유(`info`)
- 시드 변경 시 마이그레이션이 새로 생성되므로 주의

### 삭제 규칙
- Post 삭제 → Comment/Attachment 자동 삭제 (Cascade)
- Category 삭제 → 게시글 존재 시 불가 (Restrict)
- User 삭제 → Post/Comment의 UserId null 설정 (SetNull)

## 게시판 기능

- **조회수 중복 방지**: 세션 기반 (같은 세션에서 재조회 시 카운트 미증가). 단, Blazor 서킷 내 SPA 내비게이션(예: 글 등록 직후 상세 이동)에서는 응답이 이미 시작돼 신규 세션을 확립할 수 없음 — 이 경우 `IncrementViewCountAsync`가 중복 방지 기록만 생략하고 조회수 증가는 유지(`InvalidOperationException` 무시 처리). 조회수 증가 호출은 PostDetail **최초 로드 시 1회만** 수행 (댓글/추천 후 새로고침에서 호출 금지 — 중복 카운트 방지)
- **추천 중복 방지**: `PostVote` 모델로 투표 기록 저장 (로그인 사용자: UserId, 비로그인: IP 주소)
- **공지 고정**: `Post.IsPinned`/`PinnedAt` 필드. `PostService.TogglePinAsync()`로 토글 (Admin/SubAdmin 권한). 목록 정렬은 공지글(PinnedAt 최신순) → 일반글(CreatedAt 최신순), 공지글은 상단에 Primary 틴트 배경 + 좌측 액센트 바 + "공지" 칩 표시(`post-list-item-pinned`)
- **카테고리 접근 제어**: `CategoryAccess` 모델 + `CategoryAccessService` (N+1 회피 위해 전체 로딩 후 메모리 필터링). **목록(PostList)·상세(PostDetail)·수정(PostEdit)·작성(PostWrite) 페이지 모두 `CanAccessCategoryAsync`/`CanWriteToCategoryAsync` 검증 필수** — 새 게시글 노출 경로를 추가할 때 반드시 포함할 것. 홈의 최근/인기 글은 완전 공개 카테고리(`IsPublic && !RequireAuth`)만 집계
- **검색**: `/board/{slug}` 목록에서 지원. HTML 글(`IsHtml=true`)은 태그 제거본(`Post.SearchText`)으로, 레거시 평문 글은 `Content`로 검색 (`(p.SearchText ?? p.Content).Contains(q)`)
- **페이지네이션**: `PostList.razor`에서 MudPagination + 페이지 크기 선택 (기본 20). 페이지/크기/검색어는 URL 쿼리(`page`/`size`/`q`)로 보존 — 뒤로가기/새로고침 시 상태 유지
- **관리자 비밀번호 리셋**: 사용자 관리(UserList)에서 재설정 (`Components/Pages/Admin/Users/ResetPasswordDialog.razor`)
- **익명 글 수정 비밀번호 전달**: PostDetail → PostEdit 이동 시 비밀번호를 `Services/Board/PostEditVerificationState`(서킷 범위 scoped, 1회 소비)로 전달. **URL 쿼리로 비밀번호를 전달하지 말 것** (브라우저 히스토리/로그 노출). 수정 페이지 직접 진입/새로고침 시에는 상세 페이지에서 다시 비밀번호 확인 필요

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

### 미디어 첨부 (에디터)
- **이미지 = 파일 업로드 (로그인 사용자 전용)**: 게시글 툴바의 이미지 버튼 사용. `ShowInsertImageControls = isAuthenticated`로 익명에겐 버튼 미노출. Quill 기본 핸들러(base64 본문 삽입)는 `wwwroot/js/editor-media.js`의 `monsterEditor.registerImageHandler(containerId)`가 서버 업로드 방식으로 교체 — `OnAfterRenderAsync`(에디터 렌더링 후 1회)에서 등록, 에디터 초기화가 비동기라 JS 내부에서 폴링으로 인스턴스 대기. 업로드는 `POST /api/fileupload`(쿠키 인증 자동 전송, `[Authorize]`, **이미지 전용** — jpg/png/gif/webp 10MB) 경유
- **동영상 = URL 링크 임베드 (파일 업로드 미지원)**: 게시글 툴바의 동영상 버튼(`ShowEmbedVideoControls=true`, Quill 기본 핸들러) — YouTube/Vimeo 링크를 입력하면 Quill이 임베드 URL로 변환해 `<iframe class="ql-video">`로 삽입. 새니타이저가 신뢰 임베드 URL(`youtube.com/embed/`, `youtube-nocookie.com/embed/`, `player.vimeo.com/video/`)만 허용 — 그 외 iframe(`/uploads/` 포함)은 노드째 제거. 허용 도메인 추가 시 `ContentSanitizer.AllowedIframeSrcPrefixes` + 테스트 갱신
- **댓글도 동일 정책**: 댓글/답글/수정 에디터 툴바에 이미지(로그인 전용)·동영상(링크) 버튼 제공. 답글/수정 폼은 열릴 때마다 새 Quill 인스턴스가 생기므로 이미지 핸들러 등록 플래그를 `ShowReplyForm`/`StartEditComment`에서 리셋 후 `OnAfterRenderAsync`에서 재등록 (에디터 id: `comment-editor-main`/`-reply`/`-edit`)
- 업로드 검증: 확장자 화이트리스트 + 크기(10MB) + 파일 시그니처(매직넘버 — `FileUploadService.IsValidFileSignature*` 정적 메서드). 요청 본문 한도는 Kestrel/FormOptions 15MB
- 파일 저장 경로 (2단계): 업로드 시 `wwwroot/uploads/temp/` → 저장 시 `FileUploadService.MoveContentTempMediaAsync(content, postId)`가 본문 내 `/uploads/temp/` URL을 수집해 `wwwroot/uploads/posts/{postId}/`로 이동·치환 (PostService·CommentService 공용. **댓글 이미지도 부모 게시글 폴더 사용** — 게시글 삭제 시 댓글이 Cascade 삭제되므로 수명 일치. `uploads/`는 런타임 생성물 — `.gitignore` 처리, `.gitkeep`으로 폴더만 보존)
- **알려진 한계**: 저장하지 않고 이탈하면 temp 고아 이미지 잔류 (추후 청소 배치 과제), 붙여넣은 base64/외부 이미지는 저장 시 새니타이저가 노드째 제거
- Attachment 모델/테이블은 존재하나 생성 경로 없음 (본문 임베드 방식 사용 — 별도 첨부 목록 UI 없음)

### HTML 렌더링 (XSS 방어)
- **저장 시 새니타이즈 계약**: 게시글/댓글 본문(HTML)은 PostService/CommentService의 Create/Update에서 [Shared/ContentSanitizer.cs](Monster.WebApp/Monster.WebApp/Shared/ContentSanitizer.cs) `Sanitize()`를 거쳐 저장되고 `IsHtml=true`로 마킹된다. 쓰기 경로를 추가할 때 반드시 이 새니타이즈를 포함할 것.
- **출력**: [Shared/HtmlContentHelper.cs](Monster.WebApp/Monster.WebApp/Shared/HtmlContentHelper.cs)의 `ToDisplayHtml(content, isHtml)` 사용 — `IsHtml=true`면 그대로(이미 정제됨), 레거시 평문(`IsHtml=false`)이면 `ToSafeHtml()`(전체 인코딩 + 줄바꿈→`<br>`) 경유. **새니타이즈를 거치지 않은 사용자 입력을 `MarkupString`으로 직접 출력하지 말 것** — 저장형 XSS 위험.
- **화이트리스트** (ContentSanitizer — 에디터 툴바 구성과 정합 유지, 변경 시 ContentSanitizerTests 함께 갱신):
  - 태그: p, br, strong, em, u, s, h1-h3, ol, ul, li, a, img, iframe, blockquote, pre, code, span, div
  - `img` src는 `/uploads/` 상대경로만 허용 (외부 핫링크·data: base64 차단). `iframe` src는 `/uploads/` + YouTube/Vimeo 임베드 URL만 허용(`AllowedIframeSrcPrefixes`), 그 외 iframe은 노드 자체 제거
  - class는 `ql-*` 화이트리스트만, 인라인 style 전면 차단, 스킴은 http/https만
- 본문 길이 상한: `AppConstants.ContentLimits` (게시글 100,000자 / 댓글 20,000자) — 서비스에서 새니타이즈 전 검사 + UI 제출 전 검사
- **빈 본문 차단**: 서비스에서 새니타이즈 **후** `IsEmptyHtml` 재검증 (외부 이미지만 넣은 글이 새니타이즈로 비게 되는 케이스 — UI의 사전 검사는 새니타이즈 전이라 못 거름). 위반 시 `ArgumentException` → 컴포넌트 catch가 Snackbar 표시

## 에디터 (WYSIWYG)

- **컴포넌트**: `RichTextEditor` (Spillgebees.Blazor.RichTextEditor 2.2.0, Quill 2 기반). CSS는 App.razor head의 `_content/Spillgebees.Blazor.RichTextEditor/...lib.module.css`, JS는 Blazor JS Initializer로 자동 로드
- **적용 위치**: PostWrite/PostEdit(전체 툴바 + 미디어 버튼), PostDetail 댓글/답글/수정 폼 4곳(축소 툴바: Style/List/Link + 이미지(로그인 전용)/동영상 링크)
- **필수 패턴**:
  - 제출 시 반드시 `@ref` + `await _editor.GetContentAsync()` 사용 — `@bind-Content`는 디바운스가 있어 마지막 입력이 유실될 수 있음
  - 빈 값 검사는 `HtmlContentHelper.IsEmptyHtml()` 사용 (Quill 빈 값 = `<p><br></p>`, 미디어만 있는 본문은 유효 취급)
  - **프리렌더링 게이트**: 에디터는 `_isInteractive` 플래그(OnAfterRender(firstRender)에서 true)로 감싸 인터랙티브 전환 후에만 렌더링 — 정적 렌더 단계의 JS interop dispose 오류 방지. 프리렌더 중에는 MudSkeleton 표시
  - 레거시 평문 글을 에디터에 로드할 때는 `IsHtml=false`면 `ToSafeHtml()` 변환 후 주입 (특수문자·줄바꿈 보존). 수정 저장 시 IsHtml=true로 자연 전환됨
  - `ShowInsertImageControls`는 로그인 사용자에게만 true이며 **반드시 `registerImageHandler`로 기본 핸들러를 교체할 것** (기본 동작은 base64 본문 삽입 → 저장 시 새니타이저가 제거해 이미지 유실). `ShowEmbedVideoControls`는 true(링크 임베드) — 새니타이저의 iframe 허용 목록과 정합
  - 툴바 옵션 추가 시 ContentSanitizer 화이트리스트와 정합 확인 (예: 색상 버튼 추가 → style/class 허용 필요 → XSS 검토)
- **과거 이력**: Blazored.TextEditor(구형 Quill 1.3.6)는 호환성 문제로 제거됐던 이력 있음 — 재도입 금지
