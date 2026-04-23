# TeamCityMcpServers — Implementation Plan

## Quick Start for New Session

1. Check the status table below for the current epic
2. Run `/epic-workflow:start <id>` (where `<id>` is the epic's ID from the Epic column)
3. Claude Code will read the spec, load context, enter plan mode, and create tasks
4. When implementation is done, open a new session: `/epic-workflow:wrapup <id>`
5. If stopping early: `/epic-workflow:pause`

### Epic Lifecycle

```
Not Started → In Progress → Implemented → Complete
                  ^              ^             ^
     /epic-workflow:start  /epic-workflow:start  /epic-workflow:wrapup
           (begins)           (finishes)      (independent review)
```

## Status

| Phase | Epic | Name | Status | Handoff | Implemented | Completed |
|-------|------|------|--------|---------|-------------|-----------|
| 1 | QEHRnru | Solution Scaffolding | Implemented | [handoff](session-handoffs/epic-QEHRnru-implemented.md) | 2026-04-23 | — |
| 1 | rPn7Wbh | TeamCity Client & Auth | Not Started | — | — | — |
| 2 | 8BeeXFT | Build Query Tools | Not Started | — | — | — |
| 2 | uQKcsyL | Test Results & Artifact Tools | Not Started | — | — | — |
| 3 | NOsupNS | Stdio Server | Not Started | — | — | — |
| 3 | RVTI6Ye | HTTP Server & Container | Not Started | — | — | — |

## Dependency Graph

```
QEHRnru (Solution Scaffolding)
└── rPn7Wbh (TeamCity Client & Auth)
    ├── 8BeeXFT (Build Query Tools)
    │   ├── NOsupNS (Stdio Server)
    │   └── RVTI6Ye (HTTP Server & Container)
    └── uQKcsyL (Test Results & Artifact Tools)
        ├── NOsupNS (Stdio Server)
        └── RVTI6Ye (HTTP Server & Container)
```
