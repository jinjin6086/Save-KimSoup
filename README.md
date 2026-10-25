# 김스프를 구해라

승쿠런 Windows 게임. 현재 배포 버전은 **1.15**입니다.

## 바로 플레이하기

**[승쿠런 1.15 버전 다운로드](https://github.com/jinjin6086/Save-KimSoup/releases/tag/v1.15)**

1. 위 페이지의 **Assets**에서 실행용 ZIP을 다운로드합니다. GitHub 자동 생성 `Source code` ZIP은 실행용이 아닙니다.
2. ZIP 전체를 압축 해제합니다.
3. `승쿠런 1.15 버전` 폴더의 `승쿠런 1.15 버전.exe`를 실행합니다.

Windows 64비트용이며 .NET 런타임이 포함되어 있습니다. 실행파일과 `Assets` 폴더를 함께 보관하세요.

## 1.15 변경 내용

- 늑대 플레이어 스킨: 체력 4,000, 공격력 1.5배.
- 이상진 체력 20, 시간정지 전 1.6초 준비 동작과 시계 예고.
- W/E 시간정지 리듬: 2배속 6박자, 20개 랜덤 패턴과 연속 중복 방지.
- 리듬 입력 판정 ±8프레임, 실패 시 3,000 피해, 성공 시 보스 1 피해.
- 시간정지 성공 반격의 동작·충격파·입자·타격음 연결 개선.
- 광폭화와 멘헤라 변신 시 타오르는 뱃살의 취약과 적중 누적 초기화.

기존 배경음과 효과음, 고정 음량, 강로디언 QTE 및 다른 보스 기능을 유지합니다.

## 소스와 빌드

소스는 [`src/CookieRunVS2026`](src/CookieRunVS2026)에 있습니다. 기존 프로젝트의 86개 C# 파일과 프로젝트 파일을 포함합니다.

필요 환경: **Windows, .NET 10 SDK / Windows Forms 개발 환경**.

용량이 큰 이미지·음원은 실행용 배포 ZIP에 포함했습니다. 빌드하려면 먼저 배포 ZIP을 풀고 그 안의 `Assets` 폴더 전체를 `src/CookieRunVS2026/Assets`에 복사하세요. 같은 버전의 자원을 사용해야 합니다. 자원 목록과 SHA-256은 [`release/runtime-assets.json`](release/runtime-assets.json)에서 확인할 수 있습니다.

```powershell
dotnet build src/CookieRunVS2026/CookieRunVS2026.csproj -c Release
dotnet run --project src/CookieRunVS2026/CookieRunVS2026.csproj -c Release
```

## 배포 파일 확인

- 실행용 ZIP: `승쿠런 1.15 버전.zip`
- 크기: 303,345,097 bytes
- SHA-256: `3b7149ef8b5f0276fe3091a0b32ac0f1dc95050d5af90ec84eacdf3ef32a94ef`

이 저장소에는 소스와 배포 정보를 보관하며, 실행파일은 Releases에서 제공합니다.
