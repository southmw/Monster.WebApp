# Monster.WebApp

.NET 8.0 기반의 Blazor 웹 애플리케이션입니다. Interactive Server/WebAssembly 하이브리드 렌더링 모드를 사용하여 게시판 시스템과 사용자 관리 기능을 제공합니다.

**최근 업데이트**: 2025-11-27

## 주요 기능

### 게시판 시스템
- 다중 카테고리 지원
- 게시글 CRUD (생성, 읽기, 수정, 삭제)
- 댓글 및 중첩 답글 (대댓글) 기능
- 댓글/답글 수정/삭제 기능
- 비밀번호 기반 게시글/댓글 보호
- **관리자 권한** - 모든 게시글/댓글 수정/삭제 가능 (비밀번호 불필요)
- 조회수 기능 (세션 기반 중복 방지)
- 추천 기능 (사용자/IP 기반 중복 방지)
- 페이지네이션 (20개/페이지)
- **검색 기능** - 게시글 제목/내용 검색

### 인증 및 권한 관리
- Cookie 기반 인증
- 역할 기반 접근 제어 (Admin, SubAdmin, User)
- 사용자 관리 (활성화/비활성화)
- 카테고리별 접근 권한 설정
- **로그인 보안** - 5회 실패 시 15분 잠금
- **비밀번호 정책** - 최소 8자, 대/소문자/숫자/특수문자 필수

### 관리자 기능
- 사용자 관리 (생성, 수정, 역할 할당, **비밀번호 리셋**)
- 카테고리 관리 (생성, 수정, 삭제, 활성화/비활성화)

### UI/UX 기능
- **테마 설정** - 라이트/다크 모드 전환 (localStorage 저장)
- **반응형 디자인** - MudBlazor Material Design UI
- **로그인 사용자 편의** - 댓글 작성 시 닉네임/비밀번호 자동 입력
- **비밀번호 표시/숨김** - 로그인, 회원가입, 프로필 페이지에서 눈 아이콘으로 비밀번호 확인 가능
- **엔터 키 로그인** - 로그인 화면에서 비밀번호 입력 후 엔터로 바로 로그인
- **404 페이지 개선** - MudBlazor 스타일링 및 네비게이션 버튼
- **접근 거부 페이지** - 권한 부족 시 표시

## 기술 스택

| 기술 | 버전 | 용도 |
|------|------|------|
| .NET | 8.0 | 플랫폼 |
| Blazor | Server/WebAssembly 하이브리드 | 프론트엔드 |
| MudBlazor | 7.16.0 | Material Design UI |
| Entity Framework Core | 8.0.11 | ORM |
| SQL Server | 2022 | 데이터베이스 |
| BCrypt.Net-Next | 4.0.3 | 비밀번호 해싱 |
| Serilog | 9.0.0 | 로깅 |

## 시작하기

### 필수 요구사항

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2022 (또는 호환 버전)

### 설치 및 실행

1. **저장소 클론**
   ```bash
   git clone <repository-url>
   cd Monster.WebApp
   ```

2. **데이터베이스 설정**

   `Monster.WebApp/Monster.WebApp/appsettings.json` 파일을 생성하고 연결 문자열을 설정합니다:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DATABASE;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=False;MultipleActiveResultSets=True;"
     },
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "AllowedHosts": "*"
   }
   ```

3. **NuGet 패키지 복원**
   ```bash
   dotnet restore Monster.WebApp.slnx
   ```

4. **데이터베이스 마이그레이션**
   ```bash
   dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
   ```

5. **애플리케이션 실행**
   ```bash
   dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile http
   ```

6. **브라우저에서 접속**
   - HTTP: http://localhost:5104
   - HTTPS: https://localhost:7056

### 초기 관리자 계정

애플리케이션 최초 실행 시 자동으로 생성됩니다:

| 항목 | 값 |
|------|------|
| Username | admin |
| Email | admin@southmw.com |
| Password | Admin@123! |

> **보안 주의**: 프로덕션 환경에서는 초기 비밀번호를 즉시 변경하세요.

## 프로젝트 구조

```
Monster.WebApp/
├── Monster.WebApp/                  # 서버 프로젝트 (메인 호스트)
│   ├── Components/
│   │   ├── Layout/                  # 레이아웃 컴포넌트
│   │   └── Pages/                   # 페이지 컴포넌트
│   │       ├── Account/             # 인증 페이지 (Login, Register, Logout)
│   │       ├── Admin/               # 관리자 페이지 (Users, Categories)
│   │       └── Board/               # 게시판 페이지
│   ├── Controllers/                 # API 컨트롤러
│   ├── Data/                        # DbContext
│   ├── Models/                      # 데이터 모델
│   │   ├── Auth/                    # User, Role, UserRole, CategoryAccess
│   │   └── Board/                   # Category, Post, Comment, Attachment, PostVote
│   ├── Services/                    # 비즈니스 로직
│   │   ├── Auth/                    # AuthService, UserService, RoleService
│   │   └── Board/                   # CategoryService, PostService, CommentService
│   └── Shared/                      # CustomTheme, AppConstants, PasswordValidator
└── Monster.WebApp.Client/           # 클라이언트 프로젝트 (WebAssembly)
```

## 개발 명령어

```bash
# 솔루션 빌드
dotnet build Monster.WebApp.slnx

# Clean 빌드
dotnet clean Monster.WebApp.slnx

# 새 마이그레이션 생성
dotnet ef migrations add <MigrationName> --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

# 데이터베이스 업데이트
dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
```

### 개발 서버 관리 (Windows)

```powershell
# 실행 중인 dotnet 프로세스 확인
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime

# 모든 dotnet 프로세스 종료
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 주요 페이지

### 공개 페이지
| 경로 | 설명 |
|------|------|
| `/` | 홈페이지 |
| `/board` | 게시판 카테고리 목록 |
| `/board/{slug}` | 카테고리별 게시글 목록 (검색 지원) |
| `/board/{slug}/{postId}` | 게시글 상세 (댓글/답글) |
| `/board/{slug}/{postId}/edit` | 게시글 수정 |
| `/board/{slug}/write` | 게시글 작성 |

### 인증 페이지
| 경로 | 설명 |
|------|------|
| `/account/login` | 로그인 |
| `/account/register` | 회원가입 |
| `/account/logout` | 로그아웃 |
| `/profile` | 내 프로필 |

### 관리자 페이지 (Admin 역할 필요)
| 경로 | 설명 |
|------|------|
| `/admin` | 관리자 대시보드 |
| `/admin/users` | 사용자 관리 |
| `/admin/categories` | 카테고리 관리 |
| `/admin/settings` | 설정 (관리 페이지 바로가기) |

## 보안

### 비밀번호 해싱
- BCrypt.Net-Next 4.0.3 사용
- 사용자 계정, 게시글, 댓글 비밀번호 해싱

### 설정 파일 보안
- `appsettings.json`은 Git에 추적되지 않음 (민감한 DB 자격증명 포함)

### 권한 정책
| 정책명 | 설명 |
|--------|------|
| AdminOnly | Admin 역할만 접근 가능 |
| SubAdminOrHigher | Admin 또는 SubAdmin 역할 접근 가능 |
| AuthenticatedUser | 로그인된 모든 사용자 접근 가능 |

## 로깅

Serilog를 사용하여 로깅 구현:
- **콘솔**: 실시간 로그 출력
- **파일**: `logs/log-{날짜}.txt` (일별 롤링)

## 라이선스

이 프로젝트는 개인 프로젝트입니다.

## 연락처

문의사항이 있으시면 southmw@gmail.com으로 연락주세요.
