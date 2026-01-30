# Protocol Update Summary

**Date**: 2026-01-29
**Status**: Awaiting Human Review

---

## Overview

Updated the eye-rest project's Claude Code protocol structure to be more modular and efficient, following the pattern established in `rephlo-sites`.

---

## Changes Made

### Files Created

| File | Lines | Purpose |
|------|-------|---------|
| `CLAUDE.md.new` | 102 | Lean replacement for current CLAUDE.md |
| `master-agent.md` | 469 | Master Agent orchestration protocol |
| `docs/agentic/01-architecture.md` | 189 | Architecture details (moved from CLAUDE.md) |
| `docs/agentic/02-development.md` | 313 | Development workflows (new) |
| `docs/agentic/03-testing.md` | 393 | Testing strategy (new) |

### Line Count Comparison

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| CLAUDE.md (sent every request) | 170 lines | 102 lines | **40% reduction** |
| Total documentation | 170 lines | 1,466 lines | Comprehensive coverage |

---

## Architecture Decisions

### 1. Modular CLAUDE.md

**Rationale**: The original CLAUDE.md was too long (170 lines) and sent with every instruction. By moving detailed content to on-demand phase documents, we reduce token usage per request while maintaining comprehensive documentation.

**Structure**:
- Quick overview and commands
- Phase documentation table (links to detailed docs)
- Critical warnings only
- Agent role detection

### 2. Phase-Based Documentation (`docs/agentic/`)

**Rationale**: Following the `rephlo-sites` pattern, detailed documentation is loaded on-demand based on the current task phase.

| Phase | File | When to Load |
|-------|------|--------------|
| Architecture | `01-architecture.md` | Understanding codebase |
| Development | `02-development.md` | Adding features, modifying code |
| Testing | `03-testing.md` | Writing/running tests |

### 3. Separate Master Agent Protocol

**Rationale**: Master Agent orchestration is complex and rarely needed. Keeping it separate from CLAUDE.md:
- Reduces default token usage
- Allows focused updates to orchestration logic
- Follows the established pattern from `rephlo-sites`

---

## Customizations for eye-rest

### Stack-Specific Changes

| Aspect | rephlo-sites | eye-rest |
|--------|--------------|----------|
| Stack | Node.js + React + .NET | .NET 8 + WPF only |
| Project Type | Monorepo | Single application |
| Tests | Jest + xUnit + React Testing Library | xUnit only |
| Database | PostgreSQL + Prisma | JSON config + SQLite |

### Removed/Simplified

- No database migration warnings (not applicable)
- No multi-stack separation (single .NET stack)
- No frontend-specific sections
- No API naming convention conflicts (no REST API)

### Added/Customized

- WPF-specific patterns (MVVM, DispatcherTimer, multi-monitor)
- Desktop application concerns (system tray, popups, Windows APIs)
- Performance requirements specific to desktop apps
- Thread safety model for DispatcherTimer

---

## Assumptions

1. **Agent workflow will be used**: The Master Agent protocol assumes the user will invoke `/t-as-master` for complex tasks
2. **Phase docs loaded on-demand**: Claude will read phase docs when relevant (following the phase table)
3. **Testing requirements remain**: 80% coverage target for new code

---

## Deviations from Reference Protocol

| Aspect | rephlo-sites | eye-rest | Rationale |
|--------|--------------|----------|-----------|
| Checkpoint types | 5 types (Node.js, .NET, React, Full Stack, Final) | 5 types (Services, ViewModels, UI, Full Stack, Final) | Adapted for WPF architecture |
| Test conventions | Multiple frameworks | xUnit only | Single stack simplification |
| Quick commands | npm-based | dotnet-based | .NET toolchain |

---

## Pending Actions

### For Human Review

1. **Review `CLAUDE.md.new`** - Approve the lean replacement
2. **Review `master-agent.md`** - Approve orchestration protocol
3. **Review phase docs** - Approve content organization

### After Approval

1. Replace current CLAUDE.md with CLAUDE.md.new
2. Delete CLAUDE.md.new
3. Commit changes

---

## Rollback Plan

If issues arise:
1. Keep original CLAUDE.md (currently preserved)
2. Delete new files if needed
3. Git history preserves all changes

---

## Next Steps (After Approval)

```bash
# Replace CLAUDE.md
mv CLAUDE.md CLAUDE.md.backup
mv CLAUDE.md.new CLAUDE.md

# Commit
git add -A
git commit -m "refactor: Update protocol structure for modular documentation"
```
