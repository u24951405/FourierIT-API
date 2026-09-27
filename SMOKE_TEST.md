# Pre-release smoke test

Run this before every hand-in or demo. It takes about 20 minutes and catches the "it worked yesterday" breakages:
a page that no longer loads, a role that can suddenly see (or can't see) something, an email that stops sending.

## 1. Automated pass (2 minutes)

Start the API, then from `FourierIT-API`:

```powershell
$env:SMOKE_SUPERADMIN_USER = '...'; $env:SMOKE_SUPERADMIN_PASS = '...'
$env:SMOKE_CO_USER = '...';         $env:SMOKE_CO_PASS = '...'
$env:SMOKE_OWNER_USER = '...';      $env:SMOKE_OWNER_PASS = '...'
$env:SMOKE_DEPTADMIN_USER = '...';  $env:SMOKE_DEPTADMIN_PASS = '...'
.\tools\smoke-test.ps1
```

It signs in as each role and opens the pages that role depends on (read-only). Every line should be `PASS`.
A role with no credentials set shows `SKIP`. Anything slower than a few seconds is worth a look even when it passes.

## 2. Build and tests

- [ ] `dotnet test FourierIT.API.Tests` — all pass
- [ ] `npx ng build` in `FourierIT-Angular` — builds with no errors
- [ ] The API starts with "No pending EF migrations" (or applies the new ones) and no errors in the console

## 3. Click through each role

Use a normal browser window per role (or sign out between roles). Tick each line.

### Document Owner
- [ ] Sign in; the dashboard cards open the right pages
- [ ] Upload a document with a certification date; a future date is refused
- [ ] My Documents calendar shows the upload, certification and expiry dates
- [ ] Document Requests: approve a request once a Compliance Officer has approved its documents
- [ ] "Asking for more time": give an institution more time, and decline another
- [ ] A flagged document: add a note and mark it resolved
- [ ] Reports → Activity and Compliance History show your own data

### Compliance Officer
- [ ] Review queue loads; sort by "Needed soonest" and "Waiting longest"
- [ ] Open a document with View; approve one and reject one (the owner gets the email)
- [ ] Bulk approval: tick "Select all clearly valid" and approve; anything not clearly valid stays in the queue

### Department Admin
- [ ] Department requests: route one to an owner, deny one
- [ ] Department dashboard loads

### Super Admin
- [ ] User Management: change a user's role
- [ ] System Settings: both tabs; the Help button opens the topic for the tab you are on
- [ ] System Settings → Reminders shows the three reminder settings
- [ ] Every report opens with data: Monthly, Activity, Compliance History, Certificate, Client Risk Rating, System Audit, and each Super Admin report tab
- [ ] A report PDF downloads and has a footer on every page

### Institution portal
- [ ] Open an invitation link, enter the emailed code
- [ ] Request documents; edit and cancel a waiting request
- [ ] Approved Documents: View a PDF in the page, Download it, flag a problem
- [ ] My Requests: "Ask for more time" on an approved request
- [ ] Updates show approvals, extension decisions and resolved flags

## 4. Email

- [ ] At least one email arrived today (and not in spam): a code, an approval, or a reminder
- [ ] Reminders: set "Remind owners about unanswered requests" to 12 hours on a test database and check the owner is reminded after the next half-hourly run

## 5. Before committing

- [ ] No passwords, API keys or connection strings with passwords in tracked files (`git diff --cached`)
- [ ] New migrations are shared with the team the way the team agreed
