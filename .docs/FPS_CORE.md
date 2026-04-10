# First-Person Core System

**Document Version:** 1.0  
**Last Updated:** January 18, 2026  
**Maintainer:** AI Assistant + Gameplay Lead

## Status: PLANNED

This document will detail the first-person controller implementation for TinCan.

## Expected Sections

- **Camera System** - First-person camera setup and control
- **Input Handling** - Player input for movement and looking
- **Movement Controller** - Walking, sprinting, jumping mechanics
- **Animation Integration** - Animator setup for player animations
- **Interaction System** - How players interact with objects
- **Audio System** - Footsteps, jump sounds, etc.
- **Networked Movement** - Synchronizing player position to other clients
- **View Bob & Effects** - Camera bob, damage effects, screen shake

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Overall system design
- [NETWORKING.md](./NETWORKING.md) - Network synchronization
- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines

## Expected Asset Structure

```
Assets/Scripts/Player/
├── PlayerController.cs           # Main first-person controller
├── PlayerMovement.cs             # Movement logic
├── PlayerCamera.cs               # Camera control
├── PlayerAnimator.cs             # Animation handling
└── NetworkPlayerSync.cs          # Network synchronization
```

## Creation Trigger

This document will be fully written when:
1. FPS controller development begins
2. Player movement is implemented
3. Network synchronization is added

**Check back after implementing the first-person controller for the full guide.**
