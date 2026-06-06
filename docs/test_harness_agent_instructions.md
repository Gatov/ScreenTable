# ScreenMap Test Harness Agent Instructions

This document provides instructions for an AI agent on how to independently run and verify the `ScreenMap.TestHarness` pipeline.

## Objective
The `ScreenMap.TestHarness` is designed to validate the physical-to-digital alignment and detection pipeline. It simulates a physical figurine sitting on a digital map by rendering a high-contrast mock target on the screen, capturing it via an overhead camera, and running the `FigurineDetector` pipeline to see if the physical mapping algorithms can accurately locate it.

## Prerequisites
1. **Physical Setup**: The user must have a physical camera and a display screen connected.
2. **Maps Directory**: The default location for sample maps is `G:\My Drive\AD&D\_Campaigns\Carrion Crown\Maps`. Use this path for `--maps` unless the user specifies otherwise.

## Execution
Run the test harness using the `.NET` CLI. You will need to determine the appropriate `--camera` and `--display` indices based on the user's environment.

```shell
dotnet run --project "c:\Work\ScreenTable\ScreenMap.TestHarness\ScreenMap.TestHarness.csproj" -- --camera <camera_index> --display <screen_index> --output "C:\Temp\ScreenMapTests" --maps "<path_to_maps_dir>"
```

### Optional Arguments
* `--all`: By default, the harness only runs against the first map found in the directory. Include this flag to run the test matrix against all maps.

### Auto-Tuning Test
The test harness automatically runs the `AutoTuner` pipeline as part of its cycle to simulate grid-aware tuning.
* It approximates a 30-cell wide grid across the camera frame to provide a simulated `pixelsPerCell`.
* The `AutoTuner` will sweep thresholds to find the optimum detection parameters.
* Review the standard output/error for lines prefixed with `[AutoTuner]` to verify whether tuning succeeded, the chosen threshold (`Thresh`), and the minimum size (`MinCells`).

## Verification
1. **Asynchronous Execution**: Launch the command in the background and wait for it to complete.
2. **Output Parsing**: 
   * The harness runs a 5-step placement matrix per map (`Top-Center`, `Bottom-Center`, `Left-Center`, `Right-Center`, `Random`).
   * Review the standard output or the `results.json` generated in the specified `--output` directory.
   * Verify that each run reports `OK_MATCH` and exactly `figurines=1`.
3. **Visual Audit**: 
   * Inside the output directory (e.g., `C:\Temp\ScreenMapTests\run-0001`), the harness generates a `reference-scene.png`.
   * This image contains a **Pink 50% opaque circle** showing where the mock figurine was intended to be placed, and a **Blue 50% opaque circle** showing where the detector actually localized it.
   * If there is a `POS_MISMATCH` or `MARKERS_MISSING`, prompt the user to inspect these images along with `annotated.png` (the raw camera view) to help diagnose physical glare, masking boundaries, or camera clipping problems.
