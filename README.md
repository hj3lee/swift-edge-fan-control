<img src="assets/app.png" width="64" alt="Fan icon">

# Acer Swift Edge SFE16-43 Fan Control

[English](#english) · [한국어](#한국어)

## English

A tray app for reducing fan noise on the **Acer Swift Edge SFE16-43**. Choose fixed fan levels **1–7** or a custom temperature curve, and monitor CPU temperature and both fans' RPMs.

![App preview — simulated readings](docs/preview.png)

### How it works

The app uses the signed **PawnIO driver** and its `LpcACPIEC` module to access the laptop's embedded controller (EC). It enables manual control, sends level commands to both fans, and reads back the result.

Temperature, levels and RPMs are read and logged approximately **every 10 seconds**. A sampled CPU temperature **≥90°C** or a persistent sensor/control error returns control to manufacturer automatic mode.

### How to use

1. Install [official PawnIO](https://pawnio.eu/).
2. **[Download ZIP](https://github.com/hj3lee/swift-edge-fan-control/raw/refs/heads/main/downloads/SwiftEdgeFanControl-win-x64.zip)** → **Extract All** → run `SwiftEdgeFanControl.exe` as administrator.
3. Choose **Fan curve mode**, **1–7**, or **Manufacturer default**. Use **Curve settings** to edit the curve; startup at Windows login is optional.

The ZIP/EXE is provided for convenience; reviewing and building the source yourself is recommended. **AI-generated code; no professional code review.**

### Compatibility and development

Tested on **SFE16-43 / Ray_PEU / BIOS V1.71 / Windows 11 x64**. Other Acer laptops may be adaptable with AI assistance; the required changes depend on the hardware.

1. Inspect hardware, manufacturer services and WMI interfaces with PowerShell/CIM.
2. Test OEM commands against actual RPM changes.
3. Analyze the official BIOS 1.63 package without flashing; verify EC controls on V1.71 hardware.
4. Implement PawnIO access and automatic recovery; log short tests and longer holds.
5. Add the tray UI, curves, startup option and tests. [Detailed workflow (Korean)](docs/PORTING_WITH_AI.ko.md) · [Validation](docs/VALIDATION.md).

App: [MIT](LICENSE). EC module: [LGPL-2.1-or-later](vendor/NOTICE.md). Not affiliated with Acer.

## 한국어

**Acer Swift Edge SFE16-43**의 팬 소음을 줄이는 트레이 앱입니다. **고정 1–7단계** 또는 사용자 온도 커브를 적용하고 CPU 온도와 양쪽 팬 RPM을 확인합니다.

### 작동 원리

서명된 **PawnIO 드라이버**와 `LpcACPIEC` 모듈을 통해 EC에 접근합니다. 수동 제어를 켜고 양쪽 팬에 단계 명령을 보낸 뒤 다시 읽어 확인합니다.

온도·단계·RPM을 약 **10초마다** 읽고 기록합니다. 측정된 CPU 온도가 **90°C 이상**이거나 센서·제어 오류가 지속되면 제조사 자동 제어로 복귀합니다.

### 사용 방법

1. [공식 PawnIO](https://pawnio.eu/)를 설치합니다.
2. **[ZIP 다운로드](https://github.com/hj3lee/swift-edge-fan-control/raw/refs/heads/main/downloads/SwiftEdgeFanControl-win-x64.zip)** → **모두 압축 풀기** → `SwiftEdgeFanControl.exe`를 관리자 권한으로 실행합니다.
3. **팬 커브 모드**, **1–7**, **제조사 기본** 중 선택합니다. **커브 설정**에서 커브를 수정하며 로그인 시 자동 실행도 선택할 수 있습니다.

편의상 ZIP/EXE를 제공하지만 소스 검토와 직접 빌드를 권장합니다. **AI로 작성되었으며 전문적인 코드 리뷰를 받지 않았습니다.**

### 호환성과 제작 과정

**SFE16-43 / Ray_PEU / BIOS V1.71 / Windows 11 x64**에서 시험했습니다. 다른 Acer 노트북도 AI와 함께 수정할 수 있겠지만 필요한 변경은 기기에 따라 다릅니다.

1. PowerShell/CIM으로 장비·제조사 서비스·WMI 인터페이스를 조사했습니다.
2. OEM 명령이 실제 RPM을 바꾸는지 시험했습니다.
3. 공식 BIOS 1.63을 설치 없이 분석하고 V1.71 장비에서 EC 제어를 검증했습니다.
4. PawnIO 접근과 자동 복귀를 구현하고 짧은 시험·장시간 유지 로그를 기록했습니다.
5. 트레이 UI·커브·자동 실행·테스트를 추가했습니다. [상세 제작 과정](docs/PORTING_WITH_AI.ko.md) · [검증 기록](docs/VALIDATION.md).

앱: [MIT](LICENSE). EC 모듈: [LGPL-2.1-or-later](vendor/NOTICE.md). Acer 공식 앱이 아닙니다.
