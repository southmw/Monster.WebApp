# Monster.WebApp

.NET 10.0 기반의 Blazor 웹 애플리케이션입니다. Interactive Server/WebAssembly 하이브리드 렌더링 모드를 사용하여 게시판 시스템과 사용자 관리 기능을 제공합니다.

## 주요 기능

### 게시판 시스템
- 다중 카테고리 지원 (자유게시판, 질문게시판, 정보공유)
- 게시글 CRUD (생성, 읽기, 수정, 삭제)
- 댓글 및 중첩 답글 (대댓글) 기능
- 비밀번호 기반 게시글/댓글 보호
- 조회수 및 추천 기능
- 페이지네이션 (20개/페이지)

### 인증 및 권한 관리
- Cookie 기반 인증
- 역할 기반 접근 제어 (Admin, SubAdmin, User)
- 사용자 관리 (활성화/비활성화)
- 카테고리별 접근 권한 설정

### 관리자 기능
- 사용자 관리 (생성, 수정, 역할 할당)
- 카테고리 관리 (생성, 수정, 삭제, 활성화/비활성화)
- 게시글 및 댓글 관리 (예정)

## 기술 스택

- **.NET 10.0** - 최신 .NET 플랫폼
- **Blazor** - Interactive Server + WebAssembly 하이브리드
- **MudBlazor 8.14.0** - Material Design UI 프레임워크
- **Entity Framework Core 10.0** - ORM
- **SQL Server 2022** - 데이터베이스
- **BCrypt.Net-Next 4.0.3** - 비밀번호 해싱

## 시작하기

### 필수 요구사항

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server 2022 (또는 호환 버전)
- Visual Studio 2022 또는 VS Code (선택사항)

### 설치 및 실행

1. **저장소 클론**
   ```bash
   git clone <repository-url>
   cd Monster.WebApp
   ```

2. **데이터베이스 설정**

   `appsettings.json` 파일을 생성합니다:
   ```bash
   cp Monster.WebApp/Monster.WebApp/appsettings.json.template Monster.WebApp/Monster.WebApp/appsettings.json
   ```

   생성된 `appsettings.json` 파일을 열어 데이터베이스 연결 문자열을 수정합니다:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DATABASE;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=False;MultipleActiveResultSets=True;"
     }
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
   # HTTP로 실행
   dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile http

   # HTTPS로 실행
   dotnet run --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj --launch-profile https
   ```

6. **브라우저에서 접속**
   - HTTP: http://localhost:5104
   - HTTPS: https://localhost:7056

### 초기 관리자 계정

애플리케이션 최초 실행 시 자동으로 생성됩니다:

- **Username**: `admin`
- **Email**: `admin@southmw.com`
- **Password**: `Admin@123!`

**⚠️ 보안 주의**: 프로덕션 환경에서는 초기 비밀번호를 즉시 변경하세요.

## 프로젝트 구조

```
Monster.WebApp/
├── Monster.WebApp/                  # 서버 프로젝트 (메인 호스트)
│   ├── Components/
│   │   ├── Layout/                  # 레이아웃 컴포넌트
│   │   └── Pages/                   # 페이지 컴포넌트
│   │       ├── Account/             # 인증 페이지
│   │       ├── Admin/               # 관리자 페이지
│   │       └── Board/               # 게시판 페이지
│   ├── Controllers/                 # API 컨트롤러
│   ├── Data/                        # DbContext
│   ├── Models/                      # 데이터 모델
│   │   ├── Auth/                    # 인증 모델
│   │   └── Board/                   # 게시판 모델
│   ├── Services/                    # 비즈니스 로직
│   │   ├── Auth/                    # 인증 서비스
│   │   └── Board/                   # 게시판 서비스
│   └── Shared/                      # 공유 리소스
└── Monster.WebApp.Client/           # 클라이언트 프로젝트 (WebAssembly)
    └── Pages/                       # WebAssembly 전용 페이지
```

## 개발

### 빌드

```bash
# 솔루션 전체 빌드
dotnet build Monster.WebApp.slnx

# Clean 빌드
dotnet clean Monster.WebApp.slnx
```

### 데이터베이스 마이그레이션

```bash
# 새 마이그레이션 생성
dotnet ef migrations add <MigrationName> --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

# 데이터베이스 업데이트
dotnet ef database update --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj

# 마이그레이션 롤백
dotnet ef database update <PreviousMigrationName> --project Monster.WebApp/Monster.WebApp/Monster.WebApp.csproj
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

## 주요 페이지

### 공개 페이지
- `/` - 홈페이지
- `/board` - 게시판 카테고리 목록
- `/board/{categorySlug}` - 카테고리별 게시글 목록
- `/board/{categorySlug}/{postId}` - 게시글 상세
- `/board/{categorySlug}/write` - 게시글 작성

### 인증 페이지
- `/account/login` - 로그인
- `/account/register` - 회원가입
- `/account/logout` - 로그아웃

### 관리자 페이지 (Admin 역할 필요)
- `/admin` - 관리자 대시보드
- `/admin/users` - 사용자 관리
- `/admin/categories` - 카테고리 관리

## 보안

### 비밀번호 해싱
- BCrypt.Net-Next 4.0.3 사용
- 사용자 계정, 게시글, 댓글 비밀번호 해싱

### 설정 파일 보안
- `appsettings.json`은 Git에 추적되지 않음 (민감한 DB 자격증명 포함)
- 템플릿 파일(`appsettings.json.template`)을 복사하여 사용

### 권한 정책
- **AdminOnly**: Admin 역할만 접근 가능
- **SubAdminOrHigher**: Admin 또는 SubAdmin 역할 접근 가능
- **AuthenticatedUser**: 로그인된 모든 사용자 접근 가능

## 문서

자세한 개발 가이드는 [CLAUDE.md](CLAUDE.md)를 참조하세요.

## 라이선스

이 프로젝트는 개인 프로젝트입니다.

## 기여

현재 개인 프로젝트로 진행 중입니다.

## 연락처

문의사항이 있으시면 southmw@gmail.com으로 연락주세요.
