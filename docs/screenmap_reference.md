# ScreenMap Project Reference

> [!NOTE]
> ScreenMap is a WinForms-based C# application designed for Tabletop Role-Playing Games (TTRPGs). It serves as a digital game board, built to run on a computer connected to a physical screen laid flat on a table (a "ScreenTable"). It provides a Game Master (GM) control interface and a synchronized Player View.

## Architecture & Components

### Core Logic & State Management
- **`GmMap.cs` (Model)**: Manages the core map state, including fog of war, markers, grid alignment (offset and cell size), and image scaling. It persists state (marks, fog history, grid settings) to a `.json` file saved alongside the original map image.
- **Message System (`Logic/Messages/`)**: Uses a decoupled message-passing architecture (`MapMessage` subclasses like `RevealAtMessage`, `GridDataMessage`, `CenterAtMessage`) to synchronize the GM's actions with the Player View.
- **`PlayerController.cs` & `PlayersMap.cs`**: Subscribes to messages from the GM view and manages the rendering of the map for the players, ensuring they only see the revealed sections.

### User Interfaces
- **`GMMainForm.cs`**: The primary interface for the Game Master. It includes tools for:
  - Loading maps.
  - Manipulating the grid (calibration).
  - Revealing/hiding the Fog of War using brushes (`FogUtil.cs`).
  - Adding points of interest/markers (`MarkingTool.cs`).
  - Configuring camera integration and web sharing.
- **`PlayersForm.cs`**: The secondary window intended to be displayed on the screen facing the players. It is remotely controlled by the GM view.

### Physical Miniatures Integration (Computer Vision)
ScreenMap includes an advanced feature to detect physical miniatures placed on the physical screen using an overhead camera.
- **OpenCvSharp & ArUco**: Utilizes OpenCV for image processing. It detects ArUco markers (fiducials) to calibrate and align the camera feed with the digital map on the screen.
- **`DetectionService.cs`**: Constantly compares (diffs) the warped camera feed against a "clean" render of the digital map (`RenderSnapshotBitmap(..., includeDetections: false)`). This allows the system to isolate and identify physical figurines placed on the screen.
- **`CameraPreviewForm.cs`**: Provides a live diagnostic preview of the camera feed and isolated figurine crops to assist the GM in aiming and configuring the camera.

### Remote Access & Web Server
- **`ScreenMapWebServer.cs`**: A lightweight local HTTP server running on port `5001`. It serves a simple HTML page that auto-refreshes (`/image.png`) to provide a real-time view of the player map for remote users. It also serves isolated crops of the detected figurines (`/figurines.png`).
- **`CloudflareTunnel.cs`**: Integrates with `cloudflared.exe` to expose the local web server to the internet via Cloudflare Tunnels (`trycloudflare.com`), generating a secure shareable link for remote players with a single click in the GM interface.

## Key Directory Structure
- `Logic/`: Contains the business logic (`GMMap.cs`, `PlayerController.cs`), web server, tunnel integration, and message definitions.
- `Logic/Camera/`: Contains the OpenCV integration (`DetectionService.cs`, `FigurineDetector.cs`, `ArucoMarkers.cs`).
- `Controls/` & `Forms/`: Custom map controls (`GMMapView.cs`, `PlayersMapView.cs`) and UI forms for the application.
