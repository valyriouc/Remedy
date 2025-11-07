# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Remedy** is a resource and reminder management system designed to solve the "save for later" problem. Unlike traditional bookmarking or watch-later systems, Remedy intelligently schedules resource consumption based on time slots, energy levels, and context, with built-in decay mechanisms to prevent list accumulation.

## Core Concept

The system addresses a temporal coordination problem: valuable information arrives at the wrong time, and our brains are unreliable at scheduling future retrieval. Remedy matches resources to predefined time slots using multi-factor scoring that considers:

- Available time duration
- User energy level
- Current context
- Resource type, difficulty, and estimated consumption time
- Priority decay over time
- User engagement patterns

## Planned Architecture

### Phase-Based Development

The README outlines a phased implementation strategy:

1. **Phase 0**: Analog validation using physical index cards (no code)
2. **Phase 1**: CLI prototype with Python + SQLite
3. **Phase 2**: Mobile app (React Native or Flutter)
4. **Phase 3**: Sync backend + browser extensions

### Core Data Model

Key entities (from README design):

- **Resource**: Saved items (videos, articles, experiments) with metadata including type, estimated time, difficulty, target timeframe, preferred time slot, energy requirements, and decay/priority scoring
- **TimeSlot**: Recurring scheduled periods with typical duration, energy level, and compatible activity types
- **UserContext**: Current state including time, energy level, available duration, and active context

### Matching Algorithm

Resources are surfaced using composite scoring based on:
- Base priority with exponential time decay
- Energy level matching
- Time slot compatibility
- Snooze penalty (geometric decay)
- Context relevance (semantic similarity)

### Priority Decay Function

Resources naturally fade unless re-engaged through:
- Exponential base decay over days
- Geometric snooze penalty
- Recency boost for recently reminded items

## Development Guidelines

### Technology Choices

**CLI Prototype (Phase 1)**:
- Python for rapid prototyping
- SQLite for local storage
- Cron-based notification system
- Command structure: `remind <action> [options]`

**Mobile App (Phase 2)**:
- React Native or Flutter (cross-platform)
- Platform-native background tasks and notifications
- Share sheet integration for resource capture

**Backend (Phase 3)**:
- Simple REST API or Firebase
- Multi-device sync
- Desktop browser extensions

### Key Technical Challenges

1. **Notification Reliability**: Mobile OS background process limitations require multiple channels and smart timing
2. **Time Estimation**: Integration with platform APIs (YouTube, article word count) plus learning from actual user consumption
3. **Context Preservation**: Rich metadata capture at save time to maintain relevance
4. **Priority Scoring**: Start simple, add ML only after sufficient data collection

### User Experience Principles

- **Minimal friction**: One-click capture with smart defaults
- **Active recall**: Require engagement before dismissal to prevent mindless swiping
- **Predefined time slots**: Not "when free" but "during my research slot"
- **Natural decay**: Resources age out unless explicitly renewed

### Success Metrics

Personal validation metrics:
- Completion rate >60% (reminded resources actually consumed)
- Relevance rate >70% (resources still relevant when prompted)
- Time slot utilization >80%
- Stable queue length <30 items
- Snooze ratio <20%

## Build and Development

**Note**: No build commands yet - project is in design phase.

When CLI prototype is implemented, expected commands:
```bash
# Save a resource
remind save <url> --type video --time 15 --slot research

# List recommended resources for current context
remind list

# Start consuming a resource
remind start <id>

# Mark as complete with rating
remind done <id> --rating 4

# Snooze a resource
remind snooze <id> --days 3

# Configure time slots and preferences
remind config
```

## Project Philosophy

- **Local-first**: User data should live on device, sync selectively
- **Behavioral learning**: System improves through usage patterns, not manual configuration
- **Multimodal design**: Visual, haptic, and audio feedback for different learning styles
- **Guilt-free disposal**: Active decay and dismissal workflows prevent accumulation anxiety
- **Context-aware**: Resources matched to current state, not just calendar time

## Related Concepts

This system intersects with several domains:
- **Temporal PKM** (Personal Knowledge Management)
- **Spaced repetition** (but for resource consumption, not memorization)
- **Attention budgeting** (allocating energy, not just time)
- **Context-aware computing** (environment-based suggestions)
