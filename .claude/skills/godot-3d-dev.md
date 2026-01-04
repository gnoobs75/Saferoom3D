---
name: godot-3d-dev
description: Godot 4.3 C# 3D game development assistant. Use for any development task in a Godot 3D project including code examination, feature planning, implementation, testing, and documentation. Triggers on requests involving C# game code, Godot nodes (Node3D, CharacterBody3D, RigidBody3D, Area3D, etc.), scene editing, bug fixes, new features, or game development workflow. Always examines CLAUDE.md for project context before starting work.
---

# Godot 4.3 C# 3D Development

## Core Workflow

Before any task, examine the project's `CLAUDE.md` file to understand current structure, conventions, and history.

### Task Complexity Decision

**Simple tasks** (single-file changes, small fixes): Proceed directly to implementation.

**Complex tasks** (new features, multi-file changes, architectural decisions): Use Plan Mode first.

## Plan Mode

For complex or unclear tasks, create a plan before coding:

1. **Architecture** - High-level approach, which nodes/scripts involved
2. **Required changes** - List scenes and scripts to modify/create
3. **Dependencies** - Godot nodes needed (CharacterBody3D, Area3D, etc.)
4. **Impact analysis** - Effects on existing systems, signals, performance
5. **Edge cases** - Potential failure points, boundary conditions
6. **Questions** - Clarifications needed before proceeding

Wait for approval or clarification before implementing.

## Implementation

Write clean C# following Godot 4.3 conventions:

- Use proper node hierarchy and scene composition
- Connect signals in `_Ready()`, disconnect in `_ExitTree()`
- Cache node references, avoid `FindChild()` in `_Process()`
- Use `IsInstanceValid()` before accessing potentially-disposed objects
- Follow existing project patterns from CLAUDE.md

### Build & Launch

```powershell
# Build (always verify "Build succeeded")
dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"

# Launch game for testing
Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--path','C:\Claude\SafeRoom3D'

# Open editor
Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--editor','--path','C:\Claude\SafeRoom3D'
```

## Unit Testing

Create tests covering:
- Core functionality
- Edge cases and boundary conditions
- State transitions
- Collision/physics interactions

Place tests in the `Tests/` directory following existing conventions.

## Documentation Updates

After implementation:

1. **CLAUDE.md** - Add/update relevant sections (new scripts, features, patterns)
2. **Code comments** - Document non-obvious logic, signal connections
3. **README.md** - Update if user-facing features changed

## Response Format

Structure responses as:

### 1. Codebase Review Summary
Brief overview of relevant findings from CLAUDE.md and examined code.

### 2. Plan Mode (if applicable)
Detailed plan with architecture, changes, and clarifying questions.

### 3. Implementation
Code changes with explanations.

### 4. Unit Tests
Test scripts created.

### 5. Documentation Updates
Updates to CLAUDE.md and other docs.

### 6. Manual Testing Recommendations
Bulleted list with test scenarios, expected behaviors, and risks.

## Quick Reference

See CLAUDE.md for:
- Performance anti-patterns to avoid
- Signal lifecycle management
- Common Godot 4.3 C# patterns
- Project-specific conventions
