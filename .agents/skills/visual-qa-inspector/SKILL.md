---
name: visual-qa-inspector
description: Performs high-precision visual QA, UI layout verification, pixel-diff comparison against reference designs, and rendering/shader artifact detection. Use when inspecting screenshots, comparing UI against Figma/mockups, or debugging visual/rendering bugs.
---

# Visual QA & Quantitative Image Inspection Protocol

## 1. Anti-Hallucination Rule (Strict Quantitative Verification)
Never rely solely on raw visual estimation for alignment, color accuracy, or dimensions. Always pair Multimodal Vision with deterministic metrics:
- **DOM/UI Elements:** Extract exact bounding boxes (`getBoundingClientRect()`), computed styles (`getComputedStyle()`), and z-index hierarchies before making layout judgments.
- **Image/Render Files:** Use Python (`PIL`, `cv2`, `numpy`) or ImageMagick via CLI to read exact image dimensions, color channels (RGBA histograms), and pixel coordinates.

## 2. Automated Pixel-Diff & Reference Comparison
When comparing an implementation screenshot against a reference mockup/baseline:
1. Normalize both images to identical viewport dimensions.
2. Execute a programmatic diff script generating:
   - An overlay heatmap highlighting mismatched regions.
   - Structural Similarity Index (SSIM) or mismatched pixel percentage.
3. Inspect the generated diff image alongside the source image to distinguish intentional dynamic content from actual layout/rendering regressions.

## 3. Multi-Viewport & State Matrix (Web / App UI)
When auditing UI layouts, capture and verify across:
- **Viewports:** Desktop (`1920x1080`), Tablet (`768x1024`), Mobile (`375x812`).
- **Visual Defects Checklist:**
  - Text truncation, unintended line wrapping, or font fallback mismatches.
  - Overlapping bounding boxes, broken flex/grid alignment, or clipped overflow.
  - Contrast ratio violations and broken dark/light theme tokens.
  - Missing assets, broken alpha channels, or stretched aspect ratios.

## 4. Game / Shader / Render Artifact Inspection
When inspecting rendered frames, textures, or shaders:
- Check alpha premultiplication, UV seam bleeding, Z-fighting, and mipmap aliasing.
- Analyze color clamping, NaN/Inf pixel propagation (black/magenta artifacts), and frame-to-frame luminance spikes.

## 5. Iterative Visual Fix Loop
1. **Capture:** Take screenshot or render frame of current state.
2. **Measure & Diagnose:** Run quantitative check + visual inspection -> Map defect to exact CSS rule, UI component, or shader/material property.
3. **Patch:** Modify only the target code block.
4. **Re-Capture & Verify:** Capture new screenshot and run diff to confirm the defect is resolved without introducing adjacent layout shifts.
