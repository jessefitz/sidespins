**MVP Design Specification (Cloud-native, manual Blob upload)**

## 1. Purpose

The Pool Observation Tool helps a player capture an **Observation** of play (practice or match), write **timestamped notes** with minimal friction, and later **review selectively** by using notes as bookmarks into a single video recording.

This MVP is cloud-native for the web app and storage. Video files are stored in **Azure Blob Storage**, but the upload process is manual (outside the app) for MVP simplicity.

---

## 2. Core Concept

### Observation

An **Observation** represents a bounded period of play that is passively observed and optionally annotated.

An Observation has:

* A beginning time
* An end time
* Zero or more notes
* Zero or one associated recording reference

---

## 3. Functional Scope (MVP)

### Included

* Single-user system (no auth or minimal auth acceptable)
* Observation lifecycle: start, active, end
* Observation labeling: Practice vs Match
* Note capture during and after an Observation
* Timestamp adjustment presets for “that just happened” notes
* Manual association of a recording stored in Azure Blob Storage
* Embedded playback with note-based seeking (bookmarks)

### Explicitly Excluded

* Multi-camera and multi-angle support
* Camera control, recording start/stop integration, or synchronization
* Automated ingest from SD card
* Automatic video alignment to Observation start/end
* AI features, shot detection, or coaching
* Sharing/collaboration
* Cloud streaming pipeline, transcoding, or adaptive bitrate

---

## 4. UX Requirements and Principles

* Starting and stopping an Observation should be **one action** each.
* Notes must be quicker to capture than skipping them.
* Notes are commonly written **after** the moment; timestamp backdating must be easy.
* Review is a separate, calmer activity: no real-time feedback.

---

## 5. Data Model

### Observation

| Field         | Type      | Notes                  |
| ------------- | --------- | ---------------------- |
| id            | UUID      | Primary identifier     |
| label         | Enum      | `practice` | `match`   |
| start_time    | Timestamp | Set at creation        |
| end_time      | Timestamp | Null while active      |
| status        | Enum      | `active` | `completed` |
| description   | Text      | Optional free text     |
| recording_ref | Object    | Nullable, 0..1         |
| created_at    | Timestamp | Audit                  |
| updated_at    | Timestamp | Audit                  |

Derived:

* `duration_seconds` = end_time − start_time (or live elapsed if active)

---

### Note

Notes may be timestamped or general.

| Field          | Type      | Notes                                                  |
| -------------- | --------- | ------------------------------------------------------ |
| id             | UUID      | Primary identifier                                     |
| observation_id | UUID      | Foreign key                                            |
| offset_seconds | Integer   | Seconds from Observation start; null for general notes |
| text           | Text      | Required                                               |
| created_at     | Timestamp | Audit                                                  |
| updated_at     | Timestamp | Audit                                                  |

Constraints:

* If `offset_seconds` is not null, it must be ≥ 0.
* For completed Observations, the UI should prevent offsets > duration, but the API may allow it if duration is unknown or recording alignment differs.

---

### RecordingRef (embedded on Observation)

A lightweight pointer to a blob-stored video.

| Field                          | Type    | Notes                                               |
| ------------------------------ | ------- | --------------------------------------------------- |
| provider                       | Enum    | Always `azure_blob` in MVP                          |
| storage_account                | String  | Optional if implied by environment                  |
| container                      | String  | Required                                            |
| blob_name                      | String  | Required (full path within container)               |
| content_type                   | String  | Optional metadata for display; expected `video/mp4` |
| recording_start_offset_seconds | Integer | Optional, default 0                                 |

Notes on `recording_start_offset_seconds`:

* Represents how many seconds after Observation start the recording begins.
* Used to align note offsets to the video timeline when seeking:

  * `seek_time = max(0, note.offset_seconds - recording_start_offset_seconds)`

MVP assumption:

* Most users start recording close enough to Observation start that this is 0.
* Field exists to future-proof without requiring UI in MVP (can be hidden or advanced).

---

## 6. Observation Lifecycle and Behavior

### 6.1 Start Observation

User flow:

1. User clicks “Start Observation”
2. User selects label: Practice or Match (required)
3. System creates Observation with:

   * start_time = now
   * status = active
   * end_time = null

UI must show:

* Label
* Start date/time
* Live elapsed timer
* Status: Active

---

### 6.2 Active Observation

While active:

* User may add notes (timestamped or general)
* Live elapsed time is visible
* Observation can be ended at any time

---

### 6.3 End Observation

User clicks “End Observation”

* System sets:

  * end_time = now
  * status = completed

Rules:

* start_time and end_time become immutable
* Notes remain editable
* RecordingRef can be attached/edited after completion

---

## 7. Notes: Capture and Timestamping

### 7.1 Add Note During Active Observation

UI defaults:

* Timestamped note with offset = current elapsed seconds

Backdating option:
Because notes are often recorded after the moment, UI includes a “Time” control with presets:

* Now
* ~30 seconds ago
* ~1 minute ago
* ~5 minutes ago
* Custom (manual seconds or mm:ss)

On save:

* offset_seconds = computed value (clamped at 0)

---

### 7.2 Add Note After Observation Completion

User may add:

* General note (offset_seconds = null)
* Timestamped note:

  * offset via mm:ss picker or numeric input

UI should display helpful bounds:

* Show total duration
* Provide a simple timeline scale if feasible (not required)

---

## 8. Recording Workflow (Manual Blob Upload)

### 8.1 Manual upload (outside app)

User uploads the MP4 to Azure Blob Storage using tools like:

* Azure Storage Explorer, azcopy, Azure Portal, etc.

User ensures:

* Blob is accessible for playback via the web app (private + SAS recommended)
* `Content-Type` is correctly set to `video/mp4` (or the app can still attempt playback, but correct type is preferred)

---

### 8.2 Attach Recording to Observation (in-app)

In the Observation detail view, the user selects “Attach Recording” and provides:

* Container
* Blob name (path)
  Optional:
* Recording start offset seconds (advanced)
* Storage account (if multiple)

System stores these values in `recording_ref`.

Constraints:

* Only one recording_ref per Observation
* Attaching a new recording_ref replaces the previous one (confirm in UI)

---

## 9. Playback and Bookmarks

### 9.1 When recording_ref is present

Observation detail view includes:

* Embedded HTML5 video player
* Notes list (sorted by offset, then created time)
* Clicking a timestamped note seeks the player:

  * `seek_time = max(0, note.offset_seconds - recording_ref.recording_start_offset_seconds)`
* Optionally auto-play after seek (MVP choice; default recommended: seek + play)

General notes (offset null):

* Do not seek; just display as text

---

### 9.2 When recording_ref is absent

Observation detail view shows:

* Notes only
* A prominent “Attach Recording” call-to-action

---

### 9.3 Access control for playback

MVP recommendation:

* Blob container is private
* App generates a read-only URL for playback (SAS) at view time

MVP allowance:

* If the user is comfortable, a public container could be used initially, but this is discouraged.

(Implementation details are not required in this spec, but the product assumes the recording can be played in-browser.)

---

## 10. Screens and UI Requirements

### 10.1 Observation List

Each row/card shows:

* Label: Practice/Match
* Start date/time
* Status: Active/Completed
* Duration:

  * Live elapsed if active
  * Total duration if completed
* Indicator if recording is attached (yes/no)

Actions:

* View
* (Optional) End Observation if active

---

### 10.2 Observation Detail

Header area:

* Label
* Start time
* End time (or Active)
* Duration (live or fixed)
* Recording status (attached/not attached)

Sections:

1. Notes

   * Add Note button
   * Notes list
2. Recording (conditional)

   * If attached: player + replace/remove recording
   * If not: attach UI

---

### 10.3 Add Note UI

Fields:

* Note text (required)
* Note timing (required choice):

  * Timestamped (default)
  * General (no timestamp)
    If Timestamped:
* Preset offsets (active observation)
* Manual mm:ss entry (active or completed)

---

### 10.4 Attach Recording UI

Fields:

* Container (required)
* Blob name/path (required)
  Optional:
* Recording start offset seconds (advanced, default 0)

Validation:

* Basic string validation only in MVP
* No requirement to verify the blob exists at attach time (optional improvement)

---

## 11. Edge Cases and Rules

* Active Observation without notes is valid.
* Completed Observation without recording is valid.
* Notes may be edited and deleted at any time.
* If a note’s computed seek time exceeds video length, player behavior is browser-dependent; UI may clamp if video duration is known.
* If the recording URL becomes invalid (blob moved/deleted), the UI should show a clear error and allow re-attaching.

---

## 12. Non-Goals (MVP)

The system will not:

* Control or start/stop the camera recording
* Automatically upload videos from devices
* Perform automated alignment or trimming
* Support multiple recordings/angles
* Provide real-time coaching or analytics
* Support sharing or collaboration workflows

---

## 13. Success Criteria

The MVP is successful if:

* A user can start and end an Observation in seconds
* Notes can be added during play without breaking flow
* After uploading and attaching a recording, note clicks reliably jump to the right moment
* The workflow is repeatable and used consistently

---

## 14. Future Extensions (Out of Scope)

* Multiple recordings per Observation (multi-angle)
* Recording ingestion from SD card or device
* Assisted alignment (set “recording starts at…” via UI + preview)
* Tagging systems (break, safety, miss, stance, mental)
* Search and filtering by tags and note text
* Sharing, export, team workflows
* Automated highlight detection
