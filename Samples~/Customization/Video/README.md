# Video Timeline Sample — Setup Guide

This document explains how to set up and use the **Video Timeline** sample located in `Samples~/Customization/Video`.

The sample supports two usage modes:

- **Bound mode (recommended):** bind the track to an existing scene `VideoPlayer`.
- **Legacy fallback mode:** if no binding is provided, each clip creates and manages its own hidden `VideoPlayer`.

It also supports `RenderTexture` in both modes.

---

## 1) Prerequisites

- Unity project with the Timeline package installed.
- The **Customization Samples** imported from Package Manager.
- At least one imported `VideoClip` asset.
- Optional, depending on setup:
  - a scene `Camera`
  - a scene `AudioSource`
  - a `RenderTexture`

After import, sample files are available under:

`Assets/Samples/Timeline/<version>/Customization Samples/Video`

---

## 2) Quick setup

1. Create (or select) a `GameObject` with a `PlayableDirector`.
2. Create/open a Timeline asset.
3. Add a **Video Track** (`VideoTrack`).
4. Add one or more clips using `VideoPlayableAsset` and assign a `VideoClip`.
5. Configure clip options in the Inspector:
   - `Mute`, `Loop`, `Preload Time`, `Aspect Ratio`
   - `Render Mode` (`CameraFarPlane`, `CameraNearPlane`, or `RenderTexture`)
   - optional `Target Camera`, `Target Texture`, `Audio Source`

---

## 3) Mode A — Bound scene VideoPlayer (recommended for RenderTexture)

Use this mode when you want Timeline to drive a `VideoPlayer` that is already configured in the scene.

### Steps

1. Create/select a scene `VideoPlayer`.
2. Configure its output as needed (for example `renderMode = RenderTexture`, `targetTexture`, `targetCamera`, etc.).
3. Bind the Timeline **Video Track** to this `VideoPlayer` in the `PlayableDirector` bindings.
4. Play or scrub the timeline.

### Behavior in bound mode

Timeline controls playback state and timing (`clip`, `time`, `speed`, `play/pause/stop`) but preserves scene output configuration.

In particular, it does **not** overwrite:

- `renderMode`
- `targetTexture`
- `targetCamera`

Also:

- preload is disabled for shared bound players,
- the scene `VideoPlayer` GameObject is never destroyed by the playable.

---

## 4) Mode B — Legacy fallback (no track binding)

If no `VideoPlayer` is bound to the track, each clip creates a hidden internal `VideoPlayer` at runtime.

### RenderTexture in fallback

1. On the clip, set `Render Mode` to `RenderTexture`.
2. Assign `Target Texture` (`ExposedReference<RenderTexture>`).
3. Play the timeline and verify output is rendered to the assigned texture.

Internal players are cleaned up when playables are destroyed.

---

## 5) Important notes and limitations

- If multiple clips overlap while using one shared bound `VideoPlayer`, conflicts may occur.
  - A warning is logged to help detect this case.
  - Prefer non-overlapping clips when using a shared bound player.
- Video blending/easing can still produce non-ideal results depending on content and playback conditions.
- Looping timelines with videos can still lead to de-sync in some scenarios.

---

## 6) Manual validation checklist

### A. Legacy non-regression

1. No track binding.
2. Play timeline.
3. Confirm playback matches expected legacy behavior.
4. Confirm hidden players are cleaned up.

### B. Bound RenderTexture

1. Configure a scene `VideoPlayer` with `RenderTexture` output.
2. Bind track to this player.
3. Play and scrub timeline.
4. Confirm:
   - video playback works,
   - `targetTexture` remains unchanged,
   - scene object is not destroyed.

### C. Fallback RenderTexture

1. No track binding.
2. Clip `Render Mode = RenderTexture`.
3. Assign `Target Texture`.
4. Confirm output appears in the texture.

### D. Overlap warning (bound shared player)

1. Create overlapping clips on one bound Video Track.
2. Confirm warning appears in Console.
