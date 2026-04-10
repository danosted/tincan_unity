# Working with AI: Real-World Examples

**Document Version:** 1.0  
**Last Updated:** January 18, 2026  
**Purpose:** Concrete examples of requesting features, fixes, and reviews from the AI assistant

---

## Example 1: Implementing a New Feature

### Scenario
You want to add a player spawn system that:
- Spawns players at team-specific spawn points
- Handles players joining mid-game
- Validates spawn positions
- Synchronizes across the network

### What You Do First
Create/update `.docs/PLAYER_SPAWNING.md` with your design:
```markdown
# Player Spawning System

## Overview
Handles networked player spawning with team-based spawn points.

## Requirements
- Spawn all players at designated spawn points
- Different spawn points per team (Team A vs Team B)
- Validate spawn positions before allowing spawn
- Support late-joiner scenarios
- Synchronize across all clients

## Design Notes
- Use INetworkSerializable for spawn data
- Validate position collision before returning to client
- Handle case where spawn point is blocked
```

### Your Request to AI
```
Task: Implement player spawn system

System: Networking

Requirements:
- Spawn players at team-specific spawn points
- Validate spawn positions are not blocked
- Handle late-joiners joining after game started
- Synchronize spawn data across all clients

Reference Docs: PLAYER_SPAWNING.md, NETWORKING.md, ARCHITECTURE.md

Code style: Follow patterns in CODE_STANDARDS.md

Notes:
- Should work with NetworkManager for player instantiation
- Late-joiner validation should use existing player positions
```

### What AI Does
1. **Reads** your `.md` files to understand requirements
2. **Creates** script files following your architecture
3. **Writes** comments explaining "why" decisions
4. **Updates** PLAYER_SPAWNING.md with implementation details
5. **Reports** progress and completion

### What You See

```csharp
/// <summary>
/// Manages player spawn points and validates spawn positions.
/// Synchronized across network for multiplayer games.
/// </summary>
public class PlayerSpawner : NetworkBehaviour
{
    /// <summary>
    /// Attempts to spawn player at team's spawn point.
    /// Validates position is not blocked before allowing spawn.
    /// </summary>
    public NetworkObject SpawnPlayer(int teamId, Vector3 requestedPosition)
    {
        // Implementation using your requirements
    }
}
```

Then you review, test, and commit. The docs are already updated.

---

## Example 2: Bug Fix

### Scenario
Players can't see each other's positions update. Network messages arrive but transforms don't sync.

### Your Request
```
Issue: Player positions not synchronizing between clients

System: Networking

Error: 
Players move on their own client but don't appear to move on other clients.
Transform updates not arriving despite RPC calls being logged.

Steps to reproduce:
1. Start host client
2. Connect second client
3. Move player with WASD
4. Second client shows no movement on first player

Expected behavior:
All clients should see all players moving in real-time.

Reference Docs: NETWORKING.md, ARCHITECTURE.md

Additional context:
Movement is working locally (we see it in our own view)
Console shows no errors
```

### What AI Does
1. **Analyzes** your NETWORKING.md to understand the sync pattern
2. **Checks** the movement code for synchronization
3. **Identifies** the issue (example: NetworkTransform not marked for sync)
4. **Creates** fix with explanation of root cause
5. **Updates** docs with the lesson learned

### Expected Response
```csharp
// The issue: NetworkTransform was created but not added to 
// the network synchronization list in PlayerMovement.cs
// 
// The fix: Ensure NetworkTransform is on the same GameObject as
// NetworkObject and is registered in the spawned prefab.

[SerializeField] private NetworkObject _networkObject;
[SerializeField] private NetworkTransform _networkTransform;  // ← Add this

private void Start()
{
    // NetworkTransform now syncs position to all clients
    _networkTransform.Sync();
}
```

---

## Example 3: Code Review

### Scenario
A team member (or you) wrote player movement code and wants feedback before committing.

### Your Request
```
Review Request: PlayerMovement.cs

Focus: 
- Correctness: Does it handle networked movement properly?
- Performance: Any obvious inefficiencies?
- Architecture: Does it follow patterns from ARCHITECTURE.md?

File: Assets/Scripts/Player/PlayerMovement.cs

Reference Docs: CODE_STANDARDS.md, ARCHITECTURE.md, FPS_CORE.md

Concerns:
- Is the input handling efficient?
- Should we cache the rigidbody?
- Are we sending too many network updates?
```

### What AI Does
1. **Reads** your architectural docs
2. **Reviews** the code against standards
3. **Identifies** issues and improvements
4. **Provides** specific suggestions with examples
5. **Explains** reasoning for each suggestion

### Expected Response
```
✅ Good:
- Properly using Rigidbody for physics
- Good separation of input and movement logic
- Comments explain non-obvious decisions

⚠️ Improvements:
1. Cache Rigidbody reference (currently calling GetComponent each frame)
   Suggestion: Cache in Start() or via Awake()

2. Network update throttling (sending 60 updates/sec is excessive)
   Reference: NETWORKING.md - Message Handling section
   Suggestion: Send updates only when position changes > 0.1 units

3. Code style: Variables should use _camelCase
   Reference: CODE_STANDARDS.md - Naming Conventions
   Suggestion: Change 'inputVector' to '_inputVector'

Overall: 8/10 - Good structure, minor optimizations needed.
```

---

## Example 4: Architecture Consultation

### Scenario
You're designing a major system (weapons, inventory, health system) and want AI input before implementation.

### Your Request
```
Architecture Review: Weapons System Design

System: Combat/Weapons

Current Design Concept:
- Each player has a Weapon component
- Weapons track ammo, fire rate, damage
- Attacks are RPC calls to all clients

Questions:
1. Should weapon data be INetworkSerializable?
2. Should we predict fire on client or wait for server?
3. How should we handle weapon switching over network?

Reference Docs: ARCHITECTURE.md, NETWORKING.md

Context:
- We want to support 4 different weapon types
- Network authority is on the player (client-owned)
- We need to prevent cheating (no client-side damage)

Please review this design and suggest improvements.
```

### What AI Does
1. **Analyzes** your architectural requirements from docs
2. **Suggests** patterns that fit your design philosophy
3. **Identifies** potential problems early
4. **References** similar patterns in the codebase
5. **Provides** example code structure

### Expected Response
```
Your design is on the right track. Here are suggestions:

✅ Recommendations:

1. Weapon Data Structure:
   Use INetworkSerializable for weapon state sync.
   Reference: NETWORKING.md - Synchronization Strategies
   
   This allows:
   - Efficient network transmission (only changed fields)
   - Client prediction for feel
   - Server validation for security

2. Fire Prediction:
   YES - predict on client for responsiveness
   BUT - server must validate before damage applies
   
   Workflow:
   Client: Fire animation + sound (immediate)
   Server: Validate shot, calculate hit, apply damage
   All Clients: Receive damage results
   
   This prevents: Wallhack cheats, aiming cheats

3. Weapon Switching:
   Use RPC for immediate visual feedback:
   - Client calls SwitchWeapon_ServerRpc(weaponId)
   - Server validates player has weapon
   - Server calls SwitchWeapon_ClientRpc(weaponId)
   - All clients see animation
   
   This ensures: Consistency, prevents invalid states

Sample Architecture:
```csharp
public class WeaponSystem : NetworkBehaviour, INetworkSerializable
{
    // Networked data for all clients
    public NetworkVariable<int> CurrentWeaponId { get; private set; }
    
    // Server validates before damage
    [ServerRpc]
    private void FireWeapon_ServerRpc(Vector3 direction)
    {
        // Validate and apply damage
    }
}
```

Shall I implement this design? I'll also update ARCHITECTURE.md 
and create WEAPONS.md with full details.
```

---

## Example 5: Refactoring Request

### Scenario
The networking code works but has grown messy. You want to clean it up without breaking functionality.

### Your Request
```
Refactoring: Network Synchronization Cleanup

System: Networking

Current Problem:
- Multiple systems each doing their own RPC calls
- PlayerMovement, PlayerHealth, PlayerInventory all have similar patterns
- Hard to maintain - changes break things

Goal:
Centralize network synchronization in a NetworkSyncManager

Reference Docs: ARCHITECTURE.md, NETWORKING.md

Constraints:
- Must not break existing functionality
- Must work with existing prefabs
- Should reduce code duplication
- Should make it easier to add new networked systems

Acceptance Criteria:
✓ All RPCs go through central manager
✓ Existing systems still work
✓ Code duplication reduced by 50%+
✓ New systems easier to add
```

### What AI Does
1. **Plans** refactoring to prevent breaking changes
2. **Creates** new centralized manager
3. **Migrates** existing code step-by-step
4. **Tests** that functionality still works
5. **Documents** the new pattern for future systems
6. **Updates** ARCHITECTURE.md with new pattern

### Expected Result
```
Refactoring complete! Here's what changed:

NEW: NetworkSyncManager.cs
- Centralized RPC management
- Pattern for adding new networked systems
- Reduces boilerplate

UPDATED: PlayerMovement.cs
- Now uses NetworkSyncManager instead of direct RPCs
- 40% less code (duplicates removed)
- Same functionality

UPDATED: PlayerHealth.cs
- Same refactoring applied
- Now consistent with movement code

UPDATED: ARCHITECTURE.md
- New section: "Network Synchronization Manager"
- Pattern examples for future systems
- Migration guide for old code

All tests pass. Ready to commit!
```

---

## Example 6: Documentation Update

### Scenario
You're implementing a feature and realize the docs don't cover something important.

### Your Request
```
Doc Update: Add FPS Controller Input Mapping

System: FPS/Player

Current Issue:
Implementing player input but INPUT_MAPPING.md doesn't exist.
Need clear documentation of:
- What inputs map to what actions (WASD, Space, etc.)
- How to rebind controls
- How inputs connect to movement code

This should be:
- Created as .docs/INPUT_MAPPING.md
- Referenced from FPS_CORE.md
- Include code examples showing where inputs are processed

Rough outline:
## Input Mapping
- WASD: Player movement
- Space: Jump
- Shift: Sprint (planned)
- Mouse: Look around
- LMB: Fire weapon (planned)

Please create this with code examples.
```

### What AI Does
1. **Creates** the `.md` file with your outline
2. **Adds** code examples and explanations
3. **Links** it from FPS_CORE.md
4. **References** it in CONTRIBUTING.md
5. **Provides** the ready-to-implement pattern

### Expected Response
```
✅ Created .docs/INPUT_MAPPING.md

The doc includes:
- Complete input mapping table
- Code examples showing input processing
- Rebinding system overview
- Integration with PlayerController

Also updated:
- FPS_CORE.md (now references INPUT_MAPPING.md)
- CONTRIBUTING.md (updated table of docs)

Ready to implement? I can create InputManager.cs 
following the patterns documented.
```

---

## Example 7: Performance Optimization

### Scenario
Game is running slow. You want AI to analyze and suggest optimizations.

### Your Request
```
Performance Analysis: Framerate Issues

System: General/All

Problem:
Game runs at 60 FPS in editor, drops to 30 FPS with 4+ players.
Seems network-related (happens online, not offline).

Data:
- Profiler shows time in NetworkTransform updates
- 4 players = 40+ network messages/frame
- Console shows no errors

Reference Docs: ARCHITECTURE.md, NETWORKING.md

Question:
Can we optimize network message frequency?
Should we add message batching?
Better prediction strategies?

Constraints:
- Must maintain sync accuracy
- Players should see smooth movement
```

### What AI Does
1. **Analyzes** network patterns from docs
2. **Identifies** inefficiencies
3. **Proposes** optimizations with trade-offs
4. **Provides** code examples
5. **Explains** impact (FPS gain, sync quality)

### Expected Response
```
Found the issue! Here are optimizations:

1. Message Throttling (Easy - 2x speed)
   Current: Send transform every frame (60/sec per player)
   Better: Send only if moved > 0.1 units
   Impact: 4 players = 10-15 msgs/frame instead of 40+
   Trade-off: Slight lag in movement (imperceptible)

2. Message Batching (Medium - 3x speed)
   Current: Each update separate message
   Better: Bundle all player updates in one message
   Impact: Reduces message overhead by 60%
   Trade-off: Minimal, better efficiency

3. Client Prediction (Advanced - 4x speed)
   Current: Snap to network position
   Better: Predict movement between updates
   Impact: Smooth movement with fewer messages
   Trade-off: Need fallback for prediction errors

Recommendation:
Implement 1 + 2 first (easy, big impact)
Then add 3 if still needed (complex but very effective)

Estimated result: 60 FPS sustained with 8 players.

Ready to implement?
```

---

## Quick Reference: Request Checklist

Before asking AI for help, include:

- [ ] **Task/Issue Description** - What you want (feature/fix/review/etc.)
- [ ] **System** - Which system does this affect?
- [ ] **Requirements** - What needs to happen?
- [ ] **Reference Docs** - Which `.md` files are relevant?
- [ ] **Context** - Any special constraints or notes?
- [ ] **Acceptance Criteria** - How to know when it's done?

### Example Format
```
Task: [What you want]
System: [Which system]
Requirements:
- Requirement 1
- Requirement 2
Reference Docs: [List relevant .md files]
Constraints/Notes: [Anything else important]
Acceptance Criteria:
✓ Criterion 1
✓ Criterion 2
```

---

## Common Patterns

### Request Implementation
```
Task: [Feature name]
System: [System name]
Requirements: [What you need]
Reference Docs: [Relevant docs]
```

### Request Bug Fix
```
Issue: [What's broken]
Error: [Error message if any]
Steps to reproduce: [How to trigger it]
Expected behavior: [What should happen]
Reference Docs: [Relevant docs]
```

### Request Code Review
```
Review Request: [Component/File]
Focus: [What to review - performance/correctness/style]
File: [Path to file]
Reference Docs: [Relevant docs]
Concerns: [Specific things you're worried about]
```

### Request Architecture Help
```
Architecture Review: [System name]
System: [Which system]
Current Design: [Your concept]
Questions: [What you need help with]
Reference Docs: [Relevant docs]
Context: [Important constraints]
```

---

## Tips for Best Results

1. **Be Specific** - "Make it work" vs "Implement spawn validation"
2. **Reference Docs** - Always link to relevant `.md` files
3. **Provide Context** - Why you need this, not just what
4. **Ask Follow-Ups** - Don't hesitate to ask clarifying questions
5. **Review Suggestions** - Check if AI recommendations match your vision
6. **Update Docs Together** - Ask AI to update docs as code is written

---

## Next Steps

- Ready to try? Reference this file when asking for help
- Questions about any example? See [CONTRIBUTING.md](.././docs/CONTRIBUTING.md)
- More detailed info? See [SETUP_AND_UPGRADES.md](.././docs/SETUP_AND_UPGRADES.md)
