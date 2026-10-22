# Acer Swift Edge SFE16-43 Fan Control

[English](#english) · [한국어](#한국어)

## English

A small Windows tray app for reducing fan noise on the **Acer Swift Edge SFE16-43**. Choose fixed fan levels **1–7** or a custom temperature curve, and monitor CPU temperature and both fan RPMs. Includes hysteresis, logs, optional startup at Windows login and six interface languages.

![App preview — simulated readings](docs/preview.png)

### How it works

The app uses the signed **PawnIO driver** and its `LpcACPIEC` module to access the laptop's embedded controller (EC). It enables manual control, sends level commands to both fans, and reads back the result. Levels are firmware steps, **not exact RPM targets**.

Temperature, levels and RPMs are read and logged approximately **every 10 seconds**. A sampled CPU temperature **≥90°C** or a persistent sensor/control error returns control to manufacturer automatic mode. An independent watchdog attempts recovery if the app stops responding. Detection follows the sampling interval; recovery is not guaranteed if the driver or Windows fails.

### How to use

1. Install [official PawnIO](https://pawnio.eu/), unless already installed. This app does not install the driver or disable Windows security features.
2. **[Download the app ZIP](https://github.com/hj3lee/swift-edge-fan-control/raw/refs/heads/main/downloads/SwiftEdgeFanControl-win-x64.zip)** → right-click → **Extract All**. Keep `SwiftEdgeFanControl.exe` and `LpcACPIEC.bin` together.
3. Run the EXE and accept the administrator prompt. Choose **Fan curve mode**, **1–7**, or **Manufacturer default**. **Curve settings** edits the curve; the default rises to level 2 at 60°C.
4. Optionally enable **Run at Windows login**. **X** hides to the tray; **Exit** restores automatic control and closes the app.

The ZIP includes a ready-to-run EXE for convenience. For safer software-handling practice, **review the source and build it yourself**; building alone does not prove safety. **This code was AI-generated and has not undergone professional code review.** The EXE is unsigned. [SHA-256 checksums](downloads/SHA256SUMS.txt) are provided.

**Build:** at the top of this repository, select **Code → Download ZIP**, extract it, open PowerShell in the extracted folder, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\release\SwiftEdgeFanControl.exe
```

Uses Windows' .NET Framework compiler; no separate SDK is needed. The build runs hardware-free tests. Actual fan control still requires PawnIO.

### Other laptops / how this was built

Control was verified on **SFE16-43 / Ray_PEU / BIOS V1.71 / Windows 11 x64**. Other configurations are blocked. Short hardware tests succeeded, but read timeouts and an unexplained external level change were observed; long-term reliability is not established. See [validation](docs/VALIDATION.md).

AI-assisted adaptation may be possible for other SFE16-43 configurations or Acer laptops. Some may need only profile changes; others may require substantial investigation. The actual process here was:

1. Query model, BIOS and WMI interfaces with PowerShell/CIM; inspect manufacturer apps, services and drivers for control paths.
2. Test OEM/WMI commands against actual RPM changes. Successful return values did not necessarily change the fans.
3. Analyze the official BIOS 1.63 package **without flashing it** to identify EC controls and sensors; separately verify findings on the installed V1.71 hardware.
4. Implement PawnIO access, automatic restoration and a watchdog. Run short tests and longer holds, logging temperature, RPM and communication errors.
5. Add the UI, curves and startup option; test policy/error handling and restrict use to the verified hardware profile.

Do not copy EC addresses blindly. See the [detailed AI workflow and example prompt (Korean)](docs/PORTING_WITH_AI.ko.md) and [architecture](docs/ARCHITECTURE.md).

App: [MIT](LICENSE). EC module: LGPL-2.1-or-later; [third-party notices](vendor/NOTICE.md). Not affiliated with Acer.

## 한국어

**Acer Swift Edge SFE16-43**의 팬 소음을 줄이기 위한 작은 Windows 트레이 앱입니다. **고정 1–7단계** 또는 사용자 온도 커브를 적용하고 CPU 온도와 양쪽 팬 RPM을 확인합니다. 히스테리시스, 로그, Windows 로그인 시 자동 실행과 6개 언어를 지원합니다.

### 어떻게 작동하나요?

서명된 **PawnIO 드라이버**와 `LpcACPIEC` 모듈을 통해 노트북의 임베디드 컨트롤러(EC)에 접근합니다. 수동 제어를 켜고 양쪽 팬에 단계 명령을 보낸 뒤, 다시 읽어 적용 여부를 확인합니다. 1–7은 펌웨어 단계이며 **정확한 RPM을 지정하는 기능은 아닙니다.**

온도·단계·RPM을 약 **10초마다** 읽고 기록합니다. 측정된 CPU 온도가 **90°C 이상**이거나 센서·제어 오류가 지속되면 제조사 자동 제어로 복귀합니다. 앱이 응답하지 않으면 독립 워치독도 복구를 시도합니다. 고온 감지는 측정 간격의 영향을 받으며, 드라이버나 Windows 자체가 멈추면 복구를 보장할 수 없습니다.

### 사용 방법

1. [공식 PawnIO](https://pawnio.eu/)를 설치합니다. 이미 설치했다면 건너뜁니다. 앱은 드라이버를 설치하거나 Windows 보안 기능을 해제하지 않습니다.
2. **[앱 ZIP 다운로드](https://github.com/hj3lee/swift-edge-fan-control/raw/refs/heads/main/downloads/SwiftEdgeFanControl-win-x64.zip)** → 파일 우클릭 → **모두 압축 풀기**. `SwiftEdgeFanControl.exe`와 `LpcACPIEC.bin`을 함께 둡니다.
3. EXE를 실행하고 관리자 권한을 허용합니다. **팬 커브 모드**, **1–7**, **제조사 기본** 중 선택합니다. **커브 설정**에서 커브를 수정하며, 기본값은 60°C부터 2단계로 올라갑니다.
4. 필요하면 **Windows 로그인 시 자동 실행**을 체크합니다. **X**는 트레이로 숨기며, **종료**는 자동 제어로 복귀하고 앱을 닫습니다.

편의상 EXE가 포함된 ZIP을 제공합니다. 악성코드 위험을 줄이는 사용 관행으로 **소스를 검토하고 직접 빌드할 것을 권장합니다.** 직접 빌드하는 것만으로 안전성이 보장되지는 않습니다. **코드는 AI로 작성되었으며 전문적인 코드 리뷰를 받지 않았습니다.** EXE는 코드 서명되지 않았으며 [SHA-256](downloads/SHA256SUMS.txt)을 제공합니다.

**직접 빌드:** 저장소 상단 **Code → Download ZIP**으로 소스를 받아 압축을 풉니다. 해당 폴더에서 PowerShell을 열고 실행합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\release\SwiftEdgeFanControl.exe
```

Windows의 .NET Framework 컴파일러를 사용하며 별도 SDK는 필요 없습니다. 빌드 시 가상 하드웨어 검사를 실행합니다. 실제 팬 제어에는 PawnIO가 필요합니다.

### 다른 노트북에도 적용할 수 있나요? 제작 과정은?

**SFE16-43 / Ray_PEU / BIOS V1.71 / Windows 11 x64**에서 제어 동작을 확인했습니다. 다른 구성은 차단합니다. 짧은 실기기 시험은 성공했지만, 읽기 타임아웃과 원인 미상의 외부 단계 변경이 관찰되어 장기 안정성이 확립된 것은 아닙니다. [검증 기록](docs/VALIDATION.md)을 참고하세요.

다른 SFE16-43 구성이나 Acer 노트북도 AI와 함께 수정할 수 있을 것으로 생각합니다. 프로필 수정만으로 가능한 경우도 있겠지만, 기기에 따라 추가 분석이 필요합니다. 실제 제작 과정은 다음과 같습니다.

1. PowerShell/CIM으로 모델·BIOS·WMI 인터페이스를 조회하고 제조사 앱·서비스·드라이버의 제어 경로를 조사했습니다.
2. OEM/WMI 명령 전후의 실제 RPM을 비교했습니다. 성공 반환값이 있어도 팬이 바뀌지 않는 것을 확인했습니다.
3. 공식 BIOS 1.63 패키지를 **설치하지 않고 분석**해 EC 제어·센서를 찾고, 설치된 V1.71 장비에서 별도로 검증했습니다.
4. PawnIO 접근과 자동 복귀·워치독을 구현하고, 짧은 시험부터 유지 시간을 늘리며 온도·RPM·통신 오류를 기록했습니다.
5. UI·커브·자동 실행을 붙이고 정책·오류 복귀를 검사한 뒤, 확인된 하드웨어만 허용했습니다.

다른 모델에 EC 주소를 그대로 복사하지 마세요. AI에게 전달할 예시 프롬프트와 단계별 절차는 [제작 워크플로우](docs/PORTING_WITH_AI.ko.md), 구현은 [구조 설명](docs/ARCHITECTURE.md)에 있습니다.

앱: [MIT](LICENSE). EC 모듈: LGPL-2.1-or-later, [별도 고지](vendor/NOTICE.md) 참고. Acer 공식 앱이 아닙니다.
