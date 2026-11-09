# Documents, service places and car history start

Date: 2026-11-09. Status: approved by the owner in chat ("yes please"); details below marked *(call)* are the
builder's calls, made because the owner asked for the build rather than more questions.

## Why
- The owner wants to keep the paperwork of a service (orders, receipts, invoices) with the record, and note where the
  service was done, reusing places they used before.
- Stats ignore everything before the first imported trip. The owner: "count from the 0 km on odometer, all intervals
  should go into stats, even those where there is no information but total km driven." Their first oil change was at
  7 350 km and the trip data starts later, so the "new car → first oil change" interval is missing entirely.

## 1. Service place
- Expenses gain `Place: string?` (≤ 100 chars, trimmed, blank → null). Shown and editable for every category except
  Fuel (Fuel already has Station) *(call)*.
- Suggestions: `GET /api/vehicles/{vid}/expenses/places` → distinct non-null places of that vehicle, most recently used
  first (case-insensitive distinct, keeps the latest spelling), max 50. The form input offers them via a datalist.
- Maintenance Services table gets a "Where" column.

## 2. Documents
- Files attached to an expense (any category — the form is shared; the owner's focus is maintenance) *(call)*.
- Accepted: PDF, JPEG, PNG, WebP, HEIC; max 20 MB per file *(call)*. Stored on this laptop in a `documents` folder next to
  the database (`<db dir>/documents/<vehicleId>/<expenseId>/<documentId>-<safe file name>`), configurable via
  `DriverBalance:DocumentsPath`. Never in git, never sent anywhere.
- Table `ExpenseDocuments` (Id, UserId, ExpenseId, FileName, ContentType, SizeBytes, StoredPath, CreatedAt), user-scoped
  like every other row.
- API: `POST /api/vehicles/{vid}/expenses/{id}/documents` (multipart, one or more files) → created documents;
  `GET /api/vehicles/{vid}/documents/{docId}` → the file inline with its content type (opens in the browser);
  `DELETE /api/vehicles/{vid}/documents/{docId}`. Expense JSON gains `documents: { id, fileName, contentType, sizeBytes }[]`.
  Deleting an expense deletes its documents and files. Bad uploads (malformed multipart, wrong type, too big) → 400
  `{errors}`, never 500.
- UI: the expense form lists attached documents (open link, remove) and has "Attach files"; files picked on a new
  record upload right after it is created. The Services table shows a paperclip with the count; clicking opens the
  record. Expenses page rows show the same count.

## 3. Car history start
- Vehicle gains `HistoryStart: DateOnly?` and `HistoryStartOdometer: int?` (default 0 when a date is set). Edited in
  Settings ("Car history start: date, odometer at that date (0 for a new car)").
- The odometer timeline gets that reading (source `HistoryStart`) when set and earlier than every other reading.
- Intervals: the "Before first <type>" period starts at `HistoryStart` (with `StartOdometer = HistoryStartOdometer`)
  when it is earlier than the data start. Statistics periods, the vehicle timeline chart and every other consumer of
  the timeline see the reading automatically.
- Periods without trips still produce stats: odometer km (from the timeline), days, km per day (already
  `odometerKm ?? tripKm`), costs; trip-derived metrics are null and render "—".
- The analysis session's `DataStart` (used for "years" periods and intervals) becomes the earliest of HistoryStart and
  the current data start.
- The owner fills in the date; nothing is assumed.

## Out of scope
- Document OCR, sharing, cloud sync. Editing an uploaded file. Places as a managed list (free text + suggestions only).
