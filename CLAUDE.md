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

# EF Core 마이그레이션
dotnet ef migrations add MigrationName --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
```

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

### 렌더링 모드
- **Server 프로젝트**: 서버 리소스(DB, 파일)가 필요한 컴포넌트, `[StreamRendering]`
- **Client 프로젝트**: `@rendermode InteractiveAuto` 또는 `InteractiveWebAssembly` 컴포넌트

### 서비스 레이어
모든 DB 작업은 서비스 클래스(`Services/`)를 통해 수행. `Program.cs`에서 `AddScoped`로 등록.

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

- **방식**: ASP.NET Core Cookie Authentication (7일 세션)
- **역할**: Admin, SubAdmin, User
- **정책**: AdminOnly, SubAdminOrHigher, AuthenticatedUser
- **관리자 권한**: 모든 게시글/댓글 삭제 가능 (비밀번호 불필요)

**초기 관리자**: admin / Admin@123! (프로덕션에서 즉시 변경 필요)

**API 엔드포인트**:
- `POST /api/auth/login`: 로그인 (`{username, password}`)
- `POST /api/auth/logout`: 로그아웃

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

### 데이터 모델
- **Auth**: User, Role, UserRole, CategoryAccess
- **Board**: Category, Post, Comment, Attachment

### 삭제 규칙
- Post 삭제 → Comment/Attachment 자동 삭제 (Cascade)
- Category 삭제 → 게시글 존재 시 불가 (Restrict)
- User 삭제 → Post/Comment의 UserId null 설정 (SetNull)

## 주요 라우팅

| 경로 | 설명 | 권한 |
|------|------|------|
| `/` | 홈페이지 | 공개 |
| `/board` | 게시판 카테고리 목록 | 공개 |
| `/board/{slug}` | 게시글 목록 (검색 지원) | 공개 |
| `/board/{slug}/{postId}` | 게시글 상세 | 공개 |
| `/board/{slug}/{postId}/edit` | 게시글 수정 | 작성자/비밀번호 |
| `/board/{slug}/write` | 게시글 작성 | 공개 |
| `/account/login` | 로그인 | 공개 |
| `/account/register` | 회원가입 | 공개 |
| `/profile` | 내 프로필 | 로그인 |
| `/admin` | 관리자 대시보드 | Admin |
| `/admin/users` | 사용자 관리 | Admin |
| `/admin/categories` | 카테고리 관리 | Admin |

## 로깅

Serilog를 사용하여 콘솔 및 파일 로깅:
- **콘솔**: 실시간 로그 출력
- **파일**: `logs/log-{날짜}.txt` (일별 롤링)

## 주요 컴포넌트 및 파일

### 에디터 및 파일 업로드
- `Services/FileUploadService.cs` - 이미지/동영상 업로드 로직
- 파일 저장 경로: `wwwroot/uploads/posts/{postId}/`
- 지원 형식: 이미지(jpg, jpeg, png, gif, webp - 최대 10MB), 동영상(mp4, webm - 최대 50MB)

### HTML 렌더링
게시글/댓글 본문은 `@((MarkupString)content)` 방식으로 HTML 렌더링

## 에디터

- **방식**: MudTextField (Lines="15") 사용하는 일반 텍스트 입력
- **WYSIWYG 에디터**: Blazored.TextEditor (Quill.js)는 호환성 문제로 제거됨

## 최근 변경사항 (2025-11-25)

### 관리자 권한 강화
- 관리자(Admin)는 모든 게시글/댓글 삭제 시 비밀번호 입력 불필요
- `AuthService.IsAdmin()` 메서드 추가
- `PostService.DeletePostAsync()`, `CommentService.DeleteCommentAsync()` 관리자 체크 추가

### 로그인/회원가입 UX 개선
- 비밀번호 표시/숨김 토글 (눈 아이콘)
- 로그인 화면 엔터 키 로그인 지원 (`Immediate="true"`, `@onkeydown`)
- 프로필 페이지 비밀번호 변경 시에도 표시/숨김 토글 적용
