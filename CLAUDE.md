# CLAUDE.md - Eye-rest Project

## Project Overview

**Eye-rest** - Cross-platform desktop app (.NET 8 + Avalonia UI) providing automated eye rest and break reminders. Supports Windows and macOS.

| Component | Purpose |
|-----------|---------|
| TimerService | Dual timers: Eye rest (20min/20sec), Break (55min/5min) |
| NotificationService | Full-screen popup management, multi-monitor |
| ApplicationOrchestrator | Central service coordinator |
| ConfigurationService | JSON settings at `%APPDATA%\EyeRest\config.json` |

---

## Quick Commands

```bash
# Build and run
dotnet build
dotnet run

# Test
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Performance

# UI tests
run-ui-tests.bat
```

---

## Phase Documentation

Load detailed docs from `docs/agentic/` based on task:

| Phase | File | When to Load |
|-------|------|--------------|
| Architecture | `01-architecture.md` | Understanding codebase, service structure, patterns |
| Development | `02-development.md` | Adding services, working with timers/popups |
| Testing | `03-testing.md` | Writing tests, coverage requirements |

---

## Legacy WPF — OBSOLETE (Read-Only)

> **The root-level WPF project (`EyeRest.csproj`) and its associated directories (`Views/`, `ViewModels/`, `Converters/`, `Resources/Themes/`) are OBSOLETE.**
>
> - **DO NOT** modify, add, or refactor any files in these directories.
> - **DO NOT** add new WPF views, view models, or services to the root project.
> - All active development targets the **Avalonia UI** project (`EyeRest.UI/`).
> - The legacy WPF code is retained for reference only and may be removed in a future cleanup.
>
> **Active projects:** `EyeRest.UI`, `EyeRest.Core`, `EyeRest.Abstractions`, `EyeRest.Platform.Windows`, `EyeRest.Platform.macOS`

---

## Critical Warnings

### Thread Safety
- **DispatcherTimer requires UI thread** - All timer operations must run on UI thread
- **Use Dispatcher.BeginInvoke** for background → UI thread operations
- **WeakEventManager** prevents memory leaks in long-running scenarios

### Performance Requirements
- **Startup**: <3 seconds
- **Memory**: <50MB idle, <100MB active
- **System Tray**: Minimize-to-tray instead of closing

---

## Key References

| Topic | Document |
|-------|----------|
| PRD | `docs/requirements.md` |
| Project Structure | `PROJECT_STRUCTURE.md` (project root) |
| Troubleshooting | `docs/troubleshooting/` folder |
| Lessons Learned | `docs/lessons-learned/` folder |

---

## Agent Role Detection

> **This section helps differentiate Master Agent from Subagents.**

### If You Are the Master Agent

You are the **Master Agent** if ANY of these are true:
- User explicitly invoked `/t-as-master`
- User said "act as master agent" or similar
- Your TodoWrite contains "MASTER AGENT MODE ACTIVE"

**Master Agent Rules:**
1. **READ** `master-agent.md` immediately for full protocol
2. **NEVER** write code directly - delegate to Implementer Agent
3. **NEVER** create tests directly - delegate to Test Analyst Agent
4. **ALWAYS** maintain orchestration state in TodoWrite
5. **After compaction**: Check TodoWrite, re-read `master-agent.md` if needed

### Explore Agent — First Step

> **When spawning an Explore agent (or starting any codebase exploration), always read `PROJECT_STRUCTURE.md` at the project root first.** This provides a comprehensive map of all projects, services, views, dependencies, and file locations — saving significant exploration time.

### If You Are a Subagent

You are a **Subagent** if ANY of these are true:
- You were spawned by the Task tool
- Your prompt includes "As [ROLE] AGENT" (e.g., "As IMPLEMENTER AGENT")
- You received a specific task assignment from the Master Agent

**Subagent Rules:**
1. **EXECUTE** your assigned task only
2. **DO NOT** orchestrate or delegate further
3. **RETURN** results to the Master Agent
4. **FOLLOW** project standards in this CLAUDE.md
