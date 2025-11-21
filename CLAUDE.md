# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

Monster.WebApp은 .NET 10.0 기반의 Blazor 애플리케이션으로, Interactive Server/WebAssembly 하이브리드 렌더링 모드를 사용합니다.

## 프로젝트 구조

### 주요 프로젝트
- **Monster.WebApp**: 서버 프로젝트 (메인 호스트)
  - 경로: `Monster.WebApp/Monster.WebApp/`
  - ASP.NET Core 웹 서버, Blazor Server 및 WebAssembly 호스팅

- **Monster.WebApp.Client**: 클라이언트 프로젝트
  - 경로: `Monster.WebApp/Monster.WebApp.Client/`
  - Blazor WebAssembly 전용 컴포넌트

### 렌더링 모드 아키텍처

이 애플리케이션은 Blazor의 하이브리드 렌더링 전략을 사용합니다:

- **Server 프로젝트** (`Monster.WebApp`)는 다음을 담당합니다:
  - HTTP 파이프라인 구성 (HTTPS 리디렉션, Antiforgery, Static Assets)
  - Razor 컴포넌트 호스팅 (Interactive Server + WebAssembly 모드)
  - 라우팅 설정 (Router는 두 어셈블리 모두 인식)

- **Client 프로젝트** (`Monster.WebApp.Client`)는 다음을 담당합니다:
  - WebAssembly에서 실행되는 컴포넌트만 포함
  - 브라우저에서 직접 실행되는 로직

### 렌더링 모드 선택 가이드

- **`@rendermode InteractiveAuto`**: 자동 선택 (첫 로드는 Server, 이후 WebAssembly)
  - 예시: [Counter.razor](Monster.WebApp/Monster.WebApp.Client/Pages/Counter.razor)

- **`[StreamRendering]`**: 서버 렌더링 + 스트리밍
  - 예시: [Weather.razor](Monster.WebApp/Monster.WebApp/Components/Pages/Weather.razor)

- **정적 렌더링**: 기본값 (렌더모드 지정 없음)

### 컴포넌트 위치 규칙

- **Server 프로젝트에 배치**:
  - 서버 리소스 접근이 필요한 컴포넌트 (데이터베이스, 파일 시스템, 서버 API)
  - 정적 렌더링 또는 StreamRendering이 필요한 페이지
  - 레이아웃 컴포넌트 (`Components/Layout/`)
  - 루트 컴포넌트 (`App.razor`, `Routes.razor`)

- **Client 프로젝트에 배치**:
  - 브라우저에서만 실행되는 인터랙티브 컴포넌트
  - `@rendermode InteractiveWebAssembly` 또는 `InteractiveAuto`를 사용하는 컴포넌트
  - 브라우저 API 사용 컴포넌트 (localStorage, geolocation 등)

## 개발 명령어

### 빌드 및 실행
```bash
# 솔루션 빌드
dotnet build Monster.WebApp.slnx

# 서버 프로젝트만 빌드
dotnet build Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

# 클라이언트 프로젝트만 빌드
dotnet build Monster.WebApp/Monster.WebApp.Client/Monster.WebApp.Client.csproj

# 개발 서버 실행 (HTTP)
dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile http

# 개발 서버 실행 (HTTPS)
dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile https
```

### 정리 및 복원
```bash
# Clean 빌드
dotnet clean Monster.WebApp.slnx

# NuGet 패키지 복원
dotnet restore Monster.WebApp.slnx
```

### 개발 서버 관리 (Windows)
```powershell
# 실행 중인 dotnet 프로세스 확인
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime

# 모든 dotnet 프로세스 종료
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

# 특정 포트 사용 프로세스 확인 및 종료
netstat -ano | findstr :5104
taskkill /PID <PID> /F
```

### 개발 서버 정보
- HTTP: http://localhost:5104
- HTTPS: https://localhost:7056

## 중요한 구성 파일

### Program.cs 설정
[Monster.WebApp/Program.cs](Monster.WebApp/Monster.WebApp/Program.cs)에서 다음을 구성합니다:
- `AddInteractiveServerComponents()`: Server 렌더링 활성화
- `AddInteractiveWebAssemblyComponents()`: WebAssembly 렌더링 활성화
- `AddAdditionalAssemblies(typeof(Client._Imports).Assembly)`: Client 프로젝트 어셈블리를 Router에 등록
- `UseWebAssemblyDebugging()`: 개발 환경에서 WebAssembly 디버깅 활성화
- `InitializeDefaultAdminAsync()`: 애플리케이션 시작 시 기본 관리자 계정 자동 생성

### _Imports.razor
- Server: [Monster.WebApp/Components/_Imports.razor](Monster.WebApp/Monster.WebApp/Components/_Imports.razor)
  - 두 네임스페이스 모두 포함 (`Monster.WebApp`, `Monster.WebApp.Client`)

- Client: [Monster.WebApp.Client/_Imports.razor](Monster.WebApp/Monster.WebApp.Client/_Imports.razor)
  - Client 네임스페이스만 포함

## 새 컴포넌트 추가 시 주의사항

1. **렌더링 모드 결정**: 서버 리소스가 필요한지, 브라우저에서만 실행되는지 판단
2. **프로젝트 선택**: 위의 "컴포넌트 위치 규칙" 참고
3. **네임스페이스**:
   - Server 컴포넌트: `Monster.WebApp.Components` 또는 하위 네임스페이스
   - Client 컴포넌트: `Monster.WebApp.Client` 또는 하위 네임스페이스
4. **라우팅**: `@page` 디렉티브 사용 시 [Routes.razor](Monster.WebApp/Monster.WebApp/Components/Routes.razor)가 자동으로 처리

## 프로젝트 특성

- **.NET 10.0**: 최신 .NET 버전 사용
- **Nullable 활성화**: 모든 참조 타입에 null 안전성 적용
- **ImplicitUsings 활성화**: 일반적인 using 문 자동 포함
- **BlazorDisableThrowNavigationException**: `true`로 설정됨

## 인증 및 권한 시스템

### Cookie 기반 인증
- **인증 방식**: ASP.NET Core Cookie Authentication
- **로그인 경로**: `/account/login`
- **로그아웃 경로**: `/account/logout`
- **접근 거부 경로**: `/account/access-denied`
- **세션 유지 기간**: 7일 (Sliding Expiration 활성화)

### 권한 정책
- **AdminOnly**: Admin 역할만 접근 가능
- **SubAdminOrHigher**: Admin 또는 SubAdmin 역할 접근 가능
- **AuthenticatedUser**: 로그인된 모든 사용자 접근 가능

### 기본 역할 (Seeded Roles)
- **Admin**: 전체 관리자 - 모든 권한
- **SubAdmin**: 서브 관리자 - 제한된 관리 권한
- **User**: 일반 사용자

### 초기 관리자 계정
애플리케이션 최초 실행 시 자동으로 생성됩니다:
- **Username**: `admin`
- **Email**: `admin@southmw.com`
- **Password**: `Admin@123!`
- **Role**: Admin
- **Display Name**: 관리자

**보안 주의**: 프로덕션 환경에서는 초기 비밀번호를 즉시 변경하세요.

### API 엔드포인트
- `POST /api/auth/login`: 로그인 (JSON: `{username, password}`)
- `POST /api/auth/logout`: 로그아웃
- `GET /api/auth/logout`: 로그아웃 (리디렉션)
- `GET /api/auth/check-user/{username}`: 사용자 확인 (디버그용 - 프로덕션에서 제거 필요)

## 데이터베이스 및 아키텍처

### Entity Framework Core
- **데이터베이스**: SQL Server 2022 (원격 호스팅)
- **연결 문자열**: `appsettings.json`의 `ConnectionStrings:DefaultConnection`
- **마이그레이션 명령어**:
  ```bash
  # 마이그레이션 생성
  dotnet ef migrations add MigrationName --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

  # 데이터베이스 업데이트
  dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

  # 마이그레이션 롤백
  dotnet ef database update PreviousMigrationName --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
  ```

### 데이터 모델 구조

#### 인증 모델 (Models/Auth/)
- **User**: 사용자 계정 (Username, Email, PasswordHash, DisplayName, IsActive)
- **Role**: 역할 정의 (Name, Description)
- **UserRole**: 사용자-역할 다대다 관계
- **CategoryAccess**: 카테고리별 접근 제어 (사용자/역할 기반)

#### 게시판 모델 (Models/Board/)
- **Category**: 게시판 카테고리 (Name, UrlSlug, Description, DisplayOrder, IsActive, IsPublic, RequireAuth)
- **Post**: 게시글 (Title, Content, Author, PasswordHash, ViewCount, RecommendCount)
- **Comment**: 댓글 및 답글 (자기 참조 관계로 중첩 답글 구현, PasswordHash)
- **Attachment**: 첨부파일 (미구현)

### 서비스 레이어 패턴
모든 데이터베이스 작업은 서비스 클래스를 통해 수행:

#### 인증 서비스 (Services/Auth/)
- `AuthService`: 회원가입, 로그인, 로그아웃, Cookie 인증
- `UserService`: 사용자 관리 (CRUD, 활성화/비활성화)
- `RoleService`: 역할 관리, 역할 할당/제거
- `CategoryAccessService`: 카테고리별 접근 권한 관리

#### 게시판 서비스 (Services/Board/)
- `CategoryService`: 카테고리 관리 (CRUD, 활성화/비활성화)
- `PostService`: 게시글 CRUD, 페이지네이션, 조회수/추천 기능, BCrypt 비밀번호 검증
- `CommentService`: 댓글/답글 CRUD, BCrypt 비밀번호 검증

서비스는 `Program.cs`에서 `AddScoped`로 등록됨.

## UI 프레임워크 (MudBlazor)

### 버전 및 호환성
- **MudBlazor 8.14.0** 사용 중
- 주의사항:
  - `Typography` 타입 미지원 (커스텀 테마에서 제거 필요)
  - `MudSnackbarProvider`의 `Position` 속성 미지원
  - `Shadows.Elevation`는 26개 값 필요 (배열 인덱스 에러 주의)
  - **다이얼로그 Cascading Parameter**: `IMudDialogInstance?` 사용 (nullable)
    ```csharp
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private void Cancel() => MudDialog?.Cancel();
    private void Submit() => MudDialog?.Close(DialogResult.Ok(true));
    ```

### 커스텀 테마
- [Shared/CustomTheme.cs](Monster.WebApp/Monster.WebApp/Shared/CustomTheme.cs)에서 Material Design 테마 정의
- 보라색 그라디언트 (#667eea → #764ba2) 기본 색상
- Glassmorphism 효과 적용
- 레이아웃 기본 border-radius: 12px

### UI 디자인 규칙
- **입력 폼**: 컴팩트 디자인 선호
  - `Margin="Margin.Dense"` 사용
  - `min-height: 40px` CSS 클래스 적용
  - 작성자/비밀번호 필드는 `xs="6" sm="3"` (그리드 25% 너비)
  - `MudGrid Spacing="1"`로 필드 간격 최소화
- **테이블**: 고정 컬럼 너비로 최적화
  - 작성자: 120px, 작성일: 140px, 조회/추천: 80px
- **시각적 구분**: 배경색과 테두리로 섹션 명확히 구분
  - 게시글 본문: 흰색 배경 (#ffffff)
  - 댓글 섹션: 회색 배경 (#f9fafb)
  - 답글: 보라색 왼쪽 테두리 (3px solid #667eea)

## 보안 고려사항

### 비밀번호 해싱
- **라이브러리**: BCrypt.Net-Next 4.0.3
- **사용처**:
  - 사용자 계정 비밀번호 (User.PasswordHash)
  - 게시글 비밀번호 (Post.PasswordHash)
  - 댓글 비밀번호 (Comment.PasswordHash)

### 데이터베이스 보안
- **연결 문자열**: appsettings.json에 저장
- **프로덕션 권장**: 환경 변수, Azure Key Vault, 또는 User Secrets 사용

### 삭제 동작 (Cascade Rules)
- **Post 삭제**: Comment/Attachment 자동 삭제 (Cascade)
- **Category 삭제**: 게시글 존재 시 삭제 불가 (Restrict)
- **User 삭제**: Post/Comment의 UserId는 null로 설정 (SetNull)
- **UserRole 삭제**: User 또는 Role 삭제 시 자동 삭제 (Cascade)

## 최근 작업 이력 (2025-11-21)

### 관리자 페이지 기능 구현

#### 1. 카테고리 관리 (Category Management)
**구현 파일:**
- `Services/Board/CategoryService.cs` - CRUD 메서드 추가
  - `CreateCategoryAsync()` - 새 카테고리 생성
  - `UpdateCategoryAsync()` - 카테고리 수정
  - `DeleteCategoryAsync()` - 카테고리 삭제 (게시글 존재 시 제한)
- `Components/Pages/Admin/Categories/CategoryList.razor` - 카테고리 목록 및 관리 페이지
- `Components/Pages/Admin/Categories/CreateCategoryDialog.razor` - 카테고리 생성 다이얼로그
- `Components/Pages/Admin/Categories/EditCategoryDialog.razor` - 카테고리 수정 다이얼로그

**기능:**
- 카테고리 목록 조회 (ID, 이름, URL Slug, 설명, 순서, 게시글 수, 상태)
- 새 카테고리 추가 (이름, URL Slug, 설명, 표시 순서)
- 카테고리 수정 (모든 필드 + 활성화 여부)
- 카테고리 삭제 (게시글 존재 시 경고 및 제한)
- 실시간 새로고침 기능

#### 2. MudBlazor 8.14.0 다이얼로그 버그 수정
**문제:**
- 다이얼로그의 "취소", "저장", "생성", "닫기" 버튼 클릭 시 반응 없음
- DB 작업은 정상 수행되나 다이얼로그가 닫히지 않음

**원인:**
- 잘못된 Cascading Parameter 타입 사용
- `MudDialogInstance` 타입은 MudBlazor 8.14.0에 존재하지 않음

**해결 방법:**
```csharp
// ❌ 이전 (잘못된 코드)
[CascadingParameter]
private MudDialogInstance MudDialog { get; set; } = default!;

// ✅ 수정 (올바른 코드)
[CascadingParameter]
private IMudDialogInstance? MudDialog { get; set; }

// 사용 예시
private void Cancel() => MudDialog?.Cancel();
private async Task Submit() => MudDialog?.Close(DialogResult.Ok(true));
```

**수정된 파일 (4개):**
- `CreateCategoryDialog.razor:47` - 카테고리 생성 다이얼로그
- `EditCategoryDialog.razor:51` - 카테고리 수정 다이얼로그
- `EditUserDialog.razor:32` - 사용자 수정 다이얼로그
- `ManageRolesDialog.razor:37` - 역할 관리 다이얼로그

**핵심 포인트:**
- MudBlazor 8.14.0에서는 `IMudDialogInstance` 인터페이스 사용
- nullable 타입(`?`)으로 선언 필수
- null-conditional 연산자(`?.`) 사용하여 안전하게 호출

#### 3. 네비게이션 메뉴 업데이트
- `Components/Layout/NavMenu.razor`에 "카테고리 관리" 링크 추가
- 관리자 메뉴 섹션에 통합

### 기술적 결정사항
1. **다이얼로그 패턴**: MudBlazor의 `IDialogService.ShowAsync<T>()` 패턴 사용
2. **상태 관리**: `StateHasChanged()` 호출로 UI 업데이트
3. **에러 처리**: 서비스 레이어에서 try-catch, 성공/실패 boolean 반환
4. **사용자 피드백**: `ISnackbar`로 작업 결과 알림 (성공/실패 메시지)

### 남은 작업
- [ ] 게시글 관리 페이지 구현
- [ ] 댓글 관리 페이지 구현
- [ ] 역할 기반 카테고리 접근 제어 UI 구현
- [ ] 첨부파일 관리 기능 구현

## 주요 페이지 및 라우팅

### 공개 페이지
- `/`: 홈페이지
- `/board`: 게시판 카테고리 목록
- `/board/{categorySlug}`: 카테고리별 게시글 목록
- `/board/{categorySlug}/{postId}`: 게시글 상세
- `/board/{categorySlug}/write`: 게시글 작성

### 인증 페이지
- `/account/login`: 로그인
- `/account/register`: 회원가입
- `/account/logout`: 로그아웃

### 관리자 페이지 (Admin 역할 필요)
- `/admin`: 관리자 대시보드
- `/admin/users`: 사용자 관리
- `/admin/categories`: 카테고리 관리

## 주요 컴포넌트 구조

```
Components/
├── Layout/
│   ├── MainLayout.razor              # 메인 레이아웃 (그라디언트 헤더)
│   └── NavMenu.razor                 # 네비게이션 메뉴
├── Pages/
│   ├── Account/
│   │   ├── Login.razor               # 로그인 페이지
│   │   ├── Register.razor            # 회원가입 페이지
│   │   └── Logout.razor              # 로그아웃 페이지
│   ├── Admin/
│   │   ├── Index.razor               # 관리자 대시보드
│   │   ├── Categories/
│   │   │   ├── CategoryList.razor   # 카테고리 관리
│   │   │   ├── CreateCategoryDialog.razor
│   │   │   └── EditCategoryDialog.razor
│   │   └── Users/
│   │       ├── UserList.razor        # 사용자 관리
│   │       ├── EditUserDialog.razor
│   │       └── ManageRolesDialog.razor
│   └── Board/
│       ├── Index.razor               # 카테고리 목록
│       ├── PostList.razor            # 게시글 목록
│       ├── PostDetail.razor          # 게시글 상세 + 댓글
│       └── PostWrite.razor           # 게시글 작성
├── App.razor                         # 루트 컴포넌트
└── Routes.razor                      # 라우터 설정
```