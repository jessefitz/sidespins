**MVP Design Specification (Cloud-native, manual Blob upload)**

## 1. Purpose

The Pool Observation Tool helps a player capture an **Observation** of play (practice or match), write **timestamped notes** with minimal friction, and later **review selectively** by using notes as bookmarks into one or more video recordings.

This MVP is cloud-native for the web app and storage. Video files are stored in **Azure Blob Storage**, but the upload process is manual (outside the app) for MVP simplicity.

---

## 2. Core Concept

### Observation

An **Observation** represents a bounded period of play that is passively observed and optionally annotated.

An Observation has:

* A beginning time
* An end time
* Zero or more notes
* Zero or more associated recording parts (sequential video segments)

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

| Field           | Type      | Notes                      |
| --------------- | --------- | -------------------------- |
| id              | UUID      | Primary identifier         |
| label           | Enum      | `practice` | `match`     |
| start_time      | Timestamp | Set at creation            |
| end_time        | Timestamp | Null while active          |
| status          | Enum      | `active` | `completed`   |
| description     | Text      | Optional free text         |
| recording_parts | Array     | List of RecordingPart, 0..n |
| created_at      | Timestamp | Audit                      |
| updated_at      | Timestamp | Audit                      |

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

### RecordingPart (array on Observation)

Supports **multiple sequential video recordings** for a single observation. Each part represents a continuous recording segment that begins where the previous part left off in the observation timeline.

| Field                | Type    | Notes                                                   |
| -------------------- | ------- | ------------------------------------------------------- |
| part_number          | Integer | Sequential identifier (1, 2, 3...)                      |
| provider             | Enum    | Always `azure_blob` in MVP                              |
| storage_account      | String  | Optional if implied by environment                      |
| container            | String  | Required                                                |
| blob_name            | String  | Required (full path within container)                   |
| content_type         | String  | Optional metadata; expected `video/mp4`                 |
| start_offset_seconds | Integer | When this part begins in observation timeline (default 0) |
| duration_seconds     | Integer | Optional duration of this part in seconds               |

**Multi-Part Timeline Model:**

* Parts are ordered by `start_offset_seconds`
* Each part begins where the previous ended in the observation timeline
* Example: 
  - Part 1: start_offset_seconds = 0, duration_seconds = 300 (covers 0-300s)
  - Part 2: start_offset_seconds = 300, duration_seconds = 240 (covers 300-540s)
  - Part 3: start_offset_seconds = 540 (covers 540s onward)

**Note Timestamp Navigation:**

When seeking to a note timestamp:

1. Find which part contains the note's `offset_seconds`:
   - Sort parts by `start_offset_seconds`
   - Find part where `offset_seconds >= part.start_offset_seconds` and `offset_seconds < next_part.start_offset_seconds`
   - Last part handles all timestamps from its start onward

2. Calculate seek position within that part:
   - `seek_time = note.offset_seconds - part.start_offset_seconds`

3. Switch video source if needed and seek to calculated position

**Auto-Transition:**

* When a video part ends, automatically load and play the next sequential part
* Provides seamless playback across multiple recording segments
* Part indicator shows current part (e.g., "Part 2 of 3")

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
* RecordingParts can be added/edited/deleted after completion

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

### 8.2 Attach Recording Parts to Observation (in-app)

In the Observation detail view, the user can manage multiple recording parts.

**Blob Picker Workflow (Implemented):**

1. **User clicks "Attach Recording"** - Opens recording management form
2. **User enters container name** (default: observations-videos) and clicks "Load Videos"
3. **System fetches available video files** via `GET /api/blobs/list?container={name}`
4. **Blob picker displays all video files** (MP4, MOV, AVI) with:
   - Checkbox for multi-select
   - File name
   - File size and last modified date
   - Search box for filtering
   - Select All / Clear Selection buttons
5. **User selects one or more videos** - Can select multiple sequential recordings at once
6. **System auto-parses filenames** matching convention `YYYY_MMDD_HHMMSS_SEQ[_custom].EXT`:
   - Extracts part number from sequence field
   - Calculates start offset from timestamp relative to observation start
   - Calculates duration based on next file's timestamp (if multiple selected)
7. **User clicks "Add Selected Videos"** - All selected parts added simultaneously
8. **Manual override available** - "Manual Entry" button reveals fields for non-standard filenames

**Filename Convention (Auto-parsing):**

The system automatically parses structured filenames to extract metadata:

Format: `YYYY_MMDD_HHMMSS_SEQ[_custom].EXT`

Example: `2018_0102_025808_002_faststart.MP4`

Field breakdown:
- `YYYY`: 4-digit year (2018)
- `MMDD`: 2-digit month + 2-digit day (0102 = January 2)
- `HHMMSS`: Time in 24-hour format (025808 = 02:58:08)
- `SEQ`: Zero-padded sequence number (002 = part 2)
- `_custom`: Optional suffix (faststart)
- `.EXT`: File extension (MP4)

When filenames match this format:
- **Part number** is auto-populated from SEQ field
- **Start offset** is auto-calculated from timestamp relative to observation start
- **Duration** is auto-calculated for sequential selections (next timestamp - current timestamp)

**Multi-Select Benefits:**

* **Bulk addition** - Select 10 video parts, click once to add all
* **Automatic sequencing** - Parts numbered and timed based on filename timestamps
* **Duration calculation** - System calculates recording duration between consecutive files
* **Timeline alignment** - All parts positioned correctly in observation timeline

**UI Features:**

* **Blob Picker**:
  - Scrollable list with checkboxes
  - Real-time search/filter
  - Selected count display
  - Batch select/deselect controls
* **Parts List**:
  - Shows all attached parts with metadata
  - Part number and parsed timestamp
  - Blob name, start offset, and duration
  - Individual delete buttons
* **Manual Override**:
  - Available via toggle button
  - For non-standard filenames
  - All fields editable

**Constraints:**

* Multiple recording parts supported per Observation
* Parts are ordered by start_offset_seconds for playback
* Each part represents a continuous segment in the observation timeline
* Blob listing requires read access to storage container

---

## 9. Playback and Bookmarks

### 9.1 When recording_parts are present

Observation detail view includes:

* Embedded HTML5 video player showing current part
* Part indicator (e.g., "Part 2 of 3") when multiple parts exist
* Notes list (sorted by offset, then created time)
* Clicking a timestamped note:
  1. Finds which part contains the timestamp
  2. Switches video source if needed
  3. Seeks within that part: `seek_time = note.offset_seconds - part.start_offset_seconds`
  4. Auto-plays

**Auto-Transition:**

* When a video part ends, automatically loads and plays the next sequential part
* Provides seamless viewing across multiple recording segments

**Part Navigation:**

* Video player maintains `requestAnimationFrame` pattern when switching parts
* Ensures proper initialization even when container was previously hidden

General notes (offset null):

* Do not seek; just display as text

---

### 9.2 When recording_parts is empty

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
