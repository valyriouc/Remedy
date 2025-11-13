# Remedy Event-Driven Notification System - Design Concept

## Executive Summary

This document outlines a comprehensive design for an **event-driven notification system** for Remedy that intelligently reminds users about saved resources based on time slots, context, energy levels, and priority decay. The system will leverage the existing offline-first architecture while adding real-time, scheduled, and context-aware notifications across multiple channels.

## Problem Statement

### Current Limitations
1. **Passive Retrieval**: Users must remember to run `remedy list` to see what to work on
2. **No Temporal Triggers**: Resources saved for "Morning Deep Work" don't automatically surface at 9 AM
3. **Priority Decay Invisible**: Resources aging out due to time decay go unnoticed
4. **Context Blindness**: System doesn't know user's current context to suggest relevant items
5. **Mobile Gap**: CLI-only interface means no notifications on phones

### User Scenarios

**Scenario 1: Time Slot Notifications**
> "I configured 'Morning Deep Work' for weekdays at 9 AM, 90 minutes. At 9 AM, I should receive a notification with my top 3 high-priority articles matching that slot."

**Scenario 2: Priority Decay Alerts**
> "I saved an article with TargetTimeframe='ThisWeek' 6 days ago. On day 6, I get a notification: 'Resource expiring soon: only 1 day left in target timeframe.'"

**Scenario 3: Context-Based Reminders**
> "I'm in my 'research' context (detected via calendar or manual trigger). System notifies me of resources saved in that context."

**Scenario 4: Completion Prompts**
> "I started a resource 2 hours ago but haven't marked it done. System sends a gentle reminder to rate and complete it."

**Scenario 5: Weekly Digest**
> "Every Sunday evening, I get a digest: '5 resources completed this week (avg rating 4.2★), 12 pending, 3 resources decaying rapidly.'"

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Event Sources                            │
├─────────────────────────────────────────────────────────────────┤
│  • CLI Commands (save/done/snooze)                              │
│  • Server Sync Events                                            │
│  • Time-based Triggers (cron/scheduler)                         │
│  • Context Changes (calendar API, location, manual)             │
│  • Priority Decay Calculator (background job)                   │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Event Bus / Queue                           │
│                  (Azure Service Bus / RabbitMQ)                 │
├─────────────────────────────────────────────────────────────────┤
│  Event Types:                                                    │
│  • ResourceSaved                                                 │
│  • ResourceCompleted                                             │
│  • ResourceSnoozed                                               │
│  • TimeSlotStarting                                              │
│  • PriorityDecayWarning                                          │
│  • ContextChanged                                                │
│  • ResourceStale                                                 │
│  • WeeklyDigestDue                                               │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              Notification Orchestrator Service                   │
├─────────────────────────────────────────────────────────────────┤
│  • Event Routing & Filtering                                    │
│  • User Preference Matching                                      │
│  • Notification Throttling & Deduplication                      │
│  • Channel Selection (email vs push vs SMS)                     │
│  • Template Rendering                                            │
│  • Delivery Tracking                                             │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Notification Channels                          │
├─────────────────────────────────────────────────────────────────┤
│  • Push Notifications (Mobile App - FCM/APNS)                   │
│  • Email (SendGrid/Mailgun)                                      │
│  • SMS (Twilio) - optional, high-priority only                  │
│  • Desktop Notifications (Windows/macOS native)                 │
│  • In-App Notifications (CLI banner, Mobile UI)                 │
│  • Webhooks (for integrations - Slack, Discord, etc.)          │
└─────────────────────────────────────────────────────────────────┘
```

### Component Breakdown

#### 1. Event Producers

**CLI Events**
- Emit events on: save, done, snooze, start commands
- Include: userId, resourceId, timestamp, metadata
- Fire-and-forget (don't block user operations)
- Queue for delivery even when offline

**Server Events**
- Emit on: sync completion, multi-device conflicts, server-side updates
- Notify CLI clients about changes from other devices

**Scheduler Service** (New Component)
- Background service running continuously
- Executes recurring checks:
  - **Time Slot Triggers**: "Is it 9 AM on a weekday? Fire TimeSlotStarting event"
  - **Priority Decay Scans**: "Which resources decayed >20% this week? Fire warnings"
  - **Stale Resource Detection**: "Resources not engaged for >30 days? Fire stale alerts"
  - **Weekly Digests**: "Sunday 6 PM? Generate digest for all active users"

**Context Detector** (New Component)
- Monitors user context changes:
  - **Calendar Integration**: Parse calendar events for context keywords
  - **Location** (optional): Home office vs coffee shop vs library
  - **Manual Context Toggle**: User sets "research mode" via CLI/app
  - **Time of Day**: Morning vs evening affects energy level
- Emits ContextChanged events

**Priority Decay Calculator** (Background Job)
- Runs hourly or daily
- Recalculates decay for all active resources
- Emits PriorityDecayWarning when resources drop below thresholds

#### 2. Event Bus

**Technology Options**

**Option A: Cloud-Native (Azure/AWS)**
- **Azure Service Bus**: Managed queue/topic system, dead-letter queues, TTL
- **AWS SQS + SNS**: Simple Queue + pub/sub topics
- **Pros**: Highly reliable, scalable, minimal infrastructure
- **Cons**: Cost, vendor lock-in, requires internet connectivity

**Option B: Self-Hosted**
- **RabbitMQ**: Industry standard, supports complex routing
- **Redis Pub/Sub + Streams**: Lightweight, fast, good for real-time
- **Kafka**: Overkill for this scale but excellent for event sourcing
- **Pros**: Full control, no recurring costs, works offline
- **Cons**: Infrastructure overhead, need to manage reliability

**Option C: Hybrid (Recommended)**
- **Local Queue (SQLite-based)**: Store events locally when offline
- **Remote Queue (Cloud)**: Forward when online
- **Best of both worlds**: Offline-first, cloud-backed reliability

**Event Schema Example**
```json
{
  "eventId": "evt_123456",
  "eventType": "TimeSlotStarting",
  "timestamp": "2025-11-13T09:00:00Z",
  "userId": "user_abc",
  "payload": {
    "timeSlotId": "guid",
    "timeSlotName": "Morning Deep Work",
    "matchedResources": [
      {
        "resourceId": "guid",
        "title": "Deep Learning Paper",
        "computedScore": 0.92,
        "estimatedTime": 45
      }
    ],
    "totalMatches": 12,
    "topCount": 3
  },
  "metadata": {
    "source": "SchedulerService",
    "priority": "normal",
    "expiresAt": "2025-11-13T09:30:00Z"
  }
}
```

#### 3. Notification Orchestrator

**Core Responsibilities**

**A. Event Routing**
- Subscribe to all event types
- Route to appropriate handlers based on event type
- Example: TimeSlotStarting → TimeSlotNotificationHandler

**B. User Preference Filtering**
```json
{
  "userId": "user_abc",
  "preferences": {
    "enableNotifications": true,
    "channels": {
      "push": { "enabled": true, "devices": ["device_1", "device_2"] },
      "email": { "enabled": true, "address": "user@example.com" },
      "sms": { "enabled": false },
      "desktop": { "enabled": true }
    },
    "eventFilters": {
      "TimeSlotStarting": { "enabled": true, "minMatches": 1 },
      "ResourceCompleted": { "enabled": false },
      "PriorityDecayWarning": { "enabled": true, "threshold": 0.3 },
      "WeeklyDigest": { "enabled": true, "dayOfWeek": "Sunday", "hour": 18 }
    },
    "quietHours": {
      "enabled": true,
      "start": "22:00",
      "end": "08:00",
      "timezone": "America/New_York"
    },
    "throttling": {
      "maxPerHour": 5,
      "maxPerDay": 20,
      "deduplicationWindow": "15m"
    }
  }
}
```

**C. Throttling & Deduplication**
- **Rate Limiting**: Don't spam user with 50 notifications/hour
- **Deduplication**: If ResourceSaved fires 3 times in 1 minute (sync loop?), send once
- **Batching**: Combine multiple low-priority events into single notification
- **Quiet Hours**: Suppress notifications during sleep hours (queue for morning)

**D. Channel Selection Logic**
```
Decision Tree:
1. Is event priority = "urgent"? → Try SMS, fallback to push
2. Is user online in mobile app? → Send push (instant)
3. Is desktop app open? → Send desktop notification
4. Is user offline? → Queue email for delivery
5. Is quiet hours active? → Queue for later or send only critical
```

**E. Template Rendering**
- Templates per event type per channel
- Example: TimeSlotStarting email template vs push template
- Personalization: User's name, resource titles, computed scores
- Actionable: Deep links back to CLI/app (`remedy://start/resource-id`)

**F. Delivery Tracking**
- Store notification delivery status in database
- Track: sent, delivered, opened, clicked, dismissed
- Retry failed deliveries (exponential backoff)
- Dead-letter queue for permanently failed notifications

#### 4. Notification Channels

**Push Notifications (Mobile)**
- **Technology**: Firebase Cloud Messaging (FCM) for Android, Apple Push Notification Service (APNS) for iOS
- **Payload**:
  ```json
  {
    "notification": {
      "title": "Morning Deep Work (9:00 AM)",
      "body": "3 resources ready: Deep Learning Paper (45 min) +2 more",
      "icon": "remedy_icon",
      "badge": 3
    },
    "data": {
      "type": "TimeSlotStarting",
      "timeSlotId": "guid",
      "deepLink": "remedy://timeslot/guid"
    }
  }
  ```
- **Actions**: "View All", "Start Top Resource", "Snooze for 1 hour"

**Email Notifications**
- **Technology**: SendGrid, Mailgun, AWS SES
- **Templates**: HTML + Plain Text fallback
- **Types**:
  - **Immediate**: Time slot starting, urgent decay warnings
  - **Daily Digest**: Morning summary of day's slots
  - **Weekly Digest**: Sunday review of week's activity
- **Unsubscribe**: One-click unsubscribe per event type
- **Deep Links**: Click resource title → `remedy://start/resource-id` (opens app)

**SMS Notifications** (Premium Feature)
- **Technology**: Twilio
- **Use Cases**: Ultra-high-priority only (e.g., "Event in 15 minutes")
- **Cost Consideration**: SMS is expensive, use sparingly
- **Example**: "Remedy: 'Team Meeting Prep' starting in 15 min (30 min slot)"

**Desktop Notifications**
- **Windows**: Windows Notification API
- **macOS**: NotificationCenter API
- **Linux**: libnotify
- **Triggered by**: CLI daemon running in background OR server webhook to local daemon
- **Actionable**: Click notification → opens terminal with `remedy list` or starts resource

**In-App Notifications**
- **CLI**: Banner message on next command execution
  ```
  ┌─────────────────────────────────────────────────────┐
  │ 🔔 Morning Deep Work starts in 15 minutes!          │
  │    3 resources ready (92% match)                    │
  │    Run: remedy list --slot "Morning Deep Work"      │
  └─────────────────────────────────────────────────────┘
  ```
- **Mobile App**: Notification center + badge count

**Webhooks** (For Power Users)
- POST notification payload to user-configured URL
- Enables Zapier/IFTTT integration
- Use cases: Send to Slack, Discord, Notion, custom scripts
- Example: "When PriorityDecayWarning, post to #reminders Slack channel"

## Event Types & Notification Strategies

### 1. TimeSlotStarting

**Trigger**: Time slot's scheduled time approaches

**Logic**:
```
At T-15 minutes before slot start time:
1. Query resources matching slot criteria (energy, type, duration)
2. Calculate ComputedScore for each using ResourceMatchingService
3. Take top N (configurable, default 3)
4. If topScore >= threshold (e.g., 0.7), fire event
5. Otherwise, notify "No high-quality matches for this slot"
```

**Notification Content**:
- **Title**: "{TimeSlot.Name} starts in 15 minutes"
- **Body**: "{N} resources ready: {Top Resource Title} ({EstimatedTime} min) +{N-1} more"
- **Actions**: "View All", "Start Top Resource", "Skip Slot"
- **Priority**: Normal
- **Channels**: Push (primary), Email (fallback), Desktop

**User Value**: Proactive reminder to use scheduled time effectively

### 2. PriorityDecayWarning

**Trigger**: Resource's priority drops below threshold OR nearing target timeframe deadline

**Logic**:
```
Daily scan at 8 AM user's local time:
1. Find resources where:
   - Priority decayed >30% in last 7 days, OR
   - TargetTimeframe deadline within 48 hours, OR
   - TimesSnoozed >= 3
2. Rank by urgency (deadline proximity + original priority)
3. Fire events for top 5 most urgent
```

**Notification Content**:
- **Title**: "Resource needs attention"
- **Body**: "{ResourceTitle} - Target: {TargetTimeframe} (2 days left)"
- **Additional Info**: "Priority dropped from 1.0 to 0.65 (snoozed 3 times)"
- **Actions**: "Do Now", "Extend Deadline", "Archive"
- **Priority**: High (if deadline <24h), Normal (otherwise)
- **Channels**: Email (primary), Push (secondary)

**Variations**:
- **Batch Mode**: If 5+ resources decaying, send digest instead of individual notifications
- **Weekly Summary**: "5 resources decaying this week" (less urgent)

**User Value**: Prevents resources from falling through cracks, surfaces forgotten items

### 3. ResourceSaved

**Trigger**: User saves new resource (via CLI or mobile app)

**Logic**:
```
On save:
1. If TargetTimeframe = "Today", schedule reminder for optimal time today
2. If PreferredTimeSlot set, confirm slot scheduled
3. If similar resources exist (URL domain match), notify potential duplicate
```

**Notification Content**:
- **Title**: "Resource saved: {Title}"
- **Body**: "Scheduled for {TimeSlotName} ({NextOccurrence})"
- **Actions**: "View Details", "Edit"
- **Priority**: Low
- **Channels**: In-app only (don't spam)

**User Value**: Confirmation, scheduling transparency

### 4. ResourceCompleted

**Trigger**: User marks resource done

**Logic**:
```
On completion:
1. If Rating provided, thank user
2. If Rating >= 4, ask "Save similar resources?"
3. If IsRecurring, schedule next occurrence
4. Update stats, show weekly progress if milestone (e.g., 10th completion)
```

**Notification Content**:
- **Title**: "Resource completed! ✓"
- **Body**: "You've completed {WeeklyCount} resources this week"
- **Actions**: "View Stats"
- **Priority**: Low
- **Channels**: In-app only

**User Value**: Positive reinforcement, progress tracking

### 5. ContextChanged

**Trigger**: User's context changes (calendar event, location, manual toggle)

**Logic**:
```
On context change to "Research":
1. Query resources with CreatedByContext = "Research"
2. Calculate relevance score (context match + time decay)
3. If top score > 0.8, fire event with top 3
```

**Notification Content**:
- **Title**: "Context: Research"
- **Body**: "3 relevant resources available"
- **Actions**: "View Resources", "Dismiss"
- **Priority**: Normal
- **Channels**: Push, Desktop

**User Value**: Surfaces contextually relevant work automatically

### 6. ResourceStale

**Trigger**: Resource saved >30 days ago, never started or completed

**Logic**:
```
Monthly scan:
1. Find resources where:
   - SavedAt > 30 days ago
   - LastReminded = null OR > 30 days ago
   - IsCompleted = false
2. Fire events for all matches
```

**Notification Content**:
- **Title**: "Stale resource: {Title}"
- **Body**: "Saved {DaysAgo} days ago, never viewed. Still relevant?"
- **Actions**: "Keep & Schedule", "Archive", "Delete"
- **Priority**: Low
- **Channels**: Email (weekly digest)

**User Value**: Prevents list bloat, encourages curation

### 7. WeeklyDigest

**Trigger**: Scheduled time (e.g., Sunday 6 PM)

**Logic**:
```
Every Sunday at 6 PM:
1. Calculate stats for past 7 days:
   - Resources completed
   - Average rating
   - Resources saved
   - Resources snoozed
   - Top time slot by completion count
2. Look ahead to next week:
   - Pending high-priority resources
   - Upcoming deadlines
3. Generate digest
```

**Notification Content**:
- **Title**: "Weekly Remedy Digest"
- **Body**:
  ```
  This week:
  ✓ 5 resources completed (avg 4.2★)
  + 8 new resources saved
  ⏰ 3 resources snoozed

  Top slot: Morning Deep Work (3 completions)

  Next week:
  🎯 12 pending resources
  ⚠️  3 deadlines approaching
  ```
- **Actions**: "View Pending", "Plan Week"
- **Priority**: Low
- **Channels**: Email (primary), Push (optional)

**User Value**: Weekly reflection, planning next week

### 8. MultiDeviceSync

**Trigger**: Resource modified on another device

**Logic**:
```
On sync:
1. Detect resources modified by other devices
2. Fire events for significant changes:
   - Completed on device B (while you had it open on A)
   - Snoozed by device B
   - Deleted by device B
```

**Notification Content**:
- **Title**: "Resource updated on {DeviceName}"
- **Body**: "{ResourceTitle} marked complete on {OtherDevice}"
- **Actions**: "View"
- **Priority**: Normal
- **Channels**: In-app

**User Value**: Multi-device awareness, prevent duplicate work

## Technical Implementation Strategies

### Strategy 1: Server-Centric (Requires Internet)

**Architecture**:
- Server runs all notification logic
- CLI/Mobile apps register device tokens with server
- Server sends notifications via FCM/APNS/Email

**Pros**:
- Centralized logic, easier to update
- Works across all devices
- Rich analytics (open rates, click-through)

**Cons**:
- Requires server connectivity
- Breaks offline-first philosophy
- Server becomes single point of failure

**Best For**: Users who primarily work online with server sync enabled

### Strategy 2: Client-Centric (Offline-First)

**Architecture**:
- CLI/Mobile apps run notification scheduler locally
- Background daemon evaluates time slot triggers
- Local notification API (desktop/mobile OS)

**Pros**:
- Works completely offline
- No server dependency
- Low latency (instant)

**Cons**:
- Logic duplicated across clients
- No cross-device coordination
- Limited analytics

**Best For**: Offline-only users or privacy-focused users

### Strategy 3: Hybrid (Recommended)

**Architecture**:
- **Client-side**: Time-critical notifications (time slots, immediate feedback)
- **Server-side**: Analytics, digests, cross-device events, priority decay calculations
- **Queue-based sync**: Client queues notification preferences, server syncs

**Workflow Example (TimeSlotStarting)**:
```
1. Client daemon detects it's 8:45 AM (T-15 min before slot)
2. Client queries local database for matching resources
3. Client calculates scores using local ResourceMatchingService
4. Client sends desktop/mobile push notification
5. Client logs event to local queue
6. Next sync: Upload event to server for analytics
7. Server updates user's "notification history" for other devices to see
```

**Pros**:
- Best of both worlds
- Works offline (degraded functionality)
- Rich when online

**Cons**:
- Complex implementation
- Need to maintain consistency between client/server logic

**Recommended Approach**: Start with client-side for MVP, add server-side enhancements later

## Data Model Extensions

### NotificationPreferences Table
```sql
CREATE TABLE NotificationPreferences (
    Id GUID PRIMARY KEY,
    UserId GUID NOT NULL,

    -- Global toggles
    EnableNotifications BOOLEAN DEFAULT TRUE,
    EnableQuietHours BOOLEAN DEFAULT FALSE,
    QuietHoursStart TIME,
    QuietHoursEnd TIME,
    Timezone TEXT DEFAULT 'UTC',

    -- Channel preferences
    EnablePushNotifications BOOLEAN DEFAULT TRUE,
    EnableEmailNotifications BOOLEAN DEFAULT TRUE,
    EnableSMSNotifications BOOLEAN DEFAULT FALSE,
    EnableDesktopNotifications BOOLEAN DEFAULT TRUE,

    -- Event preferences (JSON)
    EventFilters TEXT, -- JSON mapping event types to preferences

    -- Throttling
    MaxNotificationsPerHour INTEGER DEFAULT 5,
    MaxNotificationsPerDay INTEGER DEFAULT 20,
    DeduplicationWindowMinutes INTEGER DEFAULT 15,

    ModifiedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### NotificationHistory Table
```sql
CREATE TABLE NotificationHistory (
    Id GUID PRIMARY KEY,
    UserId GUID NOT NULL,
    EventId GUID NOT NULL,
    EventType TEXT NOT NULL,

    Channel TEXT NOT NULL, -- 'push', 'email', 'sms', 'desktop', 'in-app'

    Status TEXT NOT NULL, -- 'queued', 'sent', 'delivered', 'opened', 'clicked', 'dismissed', 'failed'

    Title TEXT,
    Body TEXT,

    SentAt DATETIME,
    DeliveredAt DATETIME,
    OpenedAt DATETIME,
    ClickedAt DATETIME,

    FailureReason TEXT,
    RetryCount INTEGER DEFAULT 0,

    Metadata TEXT, -- JSON with event payload, delivery metadata

    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### DeviceRegistration Table
```sql
CREATE TABLE DeviceRegistration (
    Id GUID PRIMARY KEY,
    UserId GUID NOT NULL,

    DeviceName TEXT NOT NULL, -- "MacBook Pro", "iPhone 12", "Windows Desktop"
    DeviceType TEXT NOT NULL, -- 'mobile', 'desktop', 'web'
    Platform TEXT NOT NULL, -- 'ios', 'android', 'windows', 'macos', 'linux'

    -- Push notification tokens
    PushToken TEXT,
    PushProvider TEXT, -- 'fcm', 'apns'

    IsActive BOOLEAN DEFAULT TRUE,
    LastSeenAt DATETIME DEFAULT CURRENT_TIMESTAMP,

    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### TimeSlotSchedule Table (Enhanced)
```sql
ALTER TABLE TimeSlot ADD COLUMN RecurrenceRule TEXT; -- iCalendar RRULE format
ALTER TABLE TimeSlot ADD COLUMN NotificationLeadMinutes INTEGER DEFAULT 15;
ALTER TABLE TimeSlot ADD COLUMN EnableNotifications BOOLEAN DEFAULT TRUE;
```

## User Configuration Interface

### CLI Commands

```bash
# View current notification settings
remedy notify settings

# Configure notifications
remedy notify configure --channels push,email --quiet-hours 22:00-08:00

# Test notification
remedy notify test --type TimeSlotStarting

# View notification history
remedy notify history --days 7

# Disable specific event type
remedy notify disable --event PriorityDecayWarning

# Register device for push notifications
remedy notify register-device --name "iPhone" --token <fcm-token>

# Manage time slot notifications
remedy config slots edit <id> --notify-before 30 --disable-notify

# Snooze all notifications temporarily
remedy notify snooze --hours 4
```

### Mobile App UI

**Settings Screen**:
```
┌─────────────────────────────────────┐
│ Notifications                        │
├─────────────────────────────────────┤
│ ☑ Enable Notifications              │
│                                      │
│ Channels:                            │
│   ☑ Push Notifications               │
│   ☑ Email                            │
│   ☐ SMS (Premium)                    │
│                                      │
│ Event Types:                         │
│   ☑ Time Slot Reminders              │
│   ☑ Priority Decay Warnings          │
│   ☐ Resource Completed               │
│   ☑ Weekly Digest                    │
│                                      │
│ Quiet Hours:                         │
│   ☑ Enabled                          │
│   Start: 10:00 PM                    │
│   End:   8:00 AM                     │
│                                      │
│ [Advanced Settings]                  │
└─────────────────────────────────────┘
```

**Advanced Settings**:
- Max notifications per hour: 5
- Deduplication window: 15 minutes
- Batch low-priority notifications
- Webhook URL (for integrations)

## Security & Privacy Considerations

### Data Privacy
1. **Opt-In**: Notifications disabled by default, user must enable
2. **Granular Control**: Per-event-type, per-channel preferences
3. **Local Storage**: Notification preferences stored locally first, synced to server
4. **No Sensitive Content**: Don't include URLs or full resource content in push notifications (just titles)
5. **Encryption**: Encrypt push tokens and email addresses in database

### Push Token Management
1. **Rotation**: Refresh tokens periodically
2. **Revocation**: Delete tokens when device unregistered
3. **Validation**: Verify tokens before sending (avoid bounces)

### Email Unsubscribe
1. **One-Click**: Unsubscribe link in every email
2. **Selective**: Unsubscribe from specific event types, not all notifications
3. **Compliance**: CAN-SPAM, GDPR compliant

### Rate Limiting
1. **Per-User Quotas**: Prevent abuse (e.g., malicious script creating 1000 resources)
2. **Global Quotas**: Protect third-party services (SendGrid daily limits)
3. **Backoff**: If user dismisses 5 notifications in a row, reduce frequency

## Scalability Considerations

### For 1,000 Users

**Load Estimates**:
- 1,000 users × 3 time slots/day × 1 notification/slot = **3,000 notifications/day**
- Peak: 8-10 AM (Morning Deep Work) = ~500 notifications in 2 hours
- Email capacity needed: 100,000 emails/month (digests + immediates)
- Push capacity: 100,000 pushes/month

**Architecture**:
- **Single Server**: Easily handled by single instance
- **Database**: SQLite sufficient
- **Queue**: In-memory queue or Redis
- **Email**: SendGrid free tier (100 emails/day) → need paid tier (~$15/month)
- **Push**: FCM/APNS free

**Cost**: ~$20/month (email service)

### For 100,000 Users

**Load Estimates**:
- 100,000 users × 3 slots/day = **300,000 notifications/day**
- Peak: 50,000 notifications in 2 hours = **7 notifications/second**
- Email: 10M emails/month
- Push: 10M pushes/month

**Architecture**:
- **Multiple Servers**: Load balanced notification orchestrators
- **Database**: PostgreSQL with read replicas
- **Queue**: Azure Service Bus or AWS SQS (managed, reliable)
- **Email**: SendGrid Pro ($90/month for 1.5M emails)
- **Push**: Still free (FCM/APNS handle billions)
- **Caching**: Redis for user preferences, device tokens

**Cost**: ~$200-300/month (server + queue + email)

### Optimization Strategies

1. **Batching**: Process notifications in batches of 100-1000
2. **Pre-computation**: Calculate time slot matches nightly, cache results
3. **Lazy Loading**: Don't calculate scores until notification needed
4. **Sharding**: Shard users by timezone (reduces peak load)
5. **CDN**: Cache email templates on CDN

## Testing Strategy

### Unit Tests
- Event routing logic
- Preference filtering
- Throttling/deduplication
- Template rendering

### Integration Tests
- End-to-end: Emit event → Receive notification
- Multi-channel: Same event to push + email
- Cross-device: Event on device A, notification on device B

### Load Tests
- Simulate 10,000 simultaneous TimeSlotStarting events
- Measure latency: event emitted → notification delivered
- Verify no lost messages

### User Testing
- **A/B Testing**: Different notification timings (T-15 vs T-5 vs T+0)
- **Opt-in Rates**: How many users enable notifications?
- **Engagement**: Do notifications increase resource completion rates?
- **Annoyance Factor**: Dismiss/snooze rates, unsubscribe rates

## Phased Rollout Plan

### Phase 1: MVP (Client-Side Only)
**Features**:
- Desktop notifications for time slots (client daemon)
- In-app notifications in CLI
- Basic preference toggles
- Local-only (no server)

**Timeline**: 2-4 weeks
**Complexity**: Medium
**Value**: Immediate, works offline

### Phase 2: Email Notifications
**Features**:
- Email integration (SendGrid)
- Daily/weekly digests
- Priority decay warnings
- Email preferences UI

**Timeline**: 2-3 weeks
**Complexity**: Low
**Value**: High (reaches users not actively using CLI)

### Phase 3: Mobile Push
**Features**:
- Mobile app development (React Native/Flutter)
- FCM/APNS integration
- Device registration
- Push notifications for all event types

**Timeline**: 6-8 weeks (includes mobile app)
**Complexity**: High
**Value**: Very High (mobile-first experience)

### Phase 4: Advanced Features
**Features**:
- Context detection (calendar API)
- Webhooks/integrations
- SMS notifications
- Advanced analytics
- Machine learning (optimize timing)

**Timeline**: Ongoing
**Complexity**: Varies
**Value**: Nice-to-have, differentiator

## Success Metrics

### Engagement Metrics
- **Notification Open Rate**: % of notifications opened within 1 hour
- **Click-Through Rate**: % of notifications that lead to action (start resource, view list)
- **Resource Completion Rate**: Does it increase after notifications enabled?
- **Time-to-Action**: How long from notification to starting resource?

### Quality Metrics
- **Dismissal Rate**: % of notifications dismissed without action (target <20%)
- **Snooze Rate**: % of time slot notifications snoozed (target <30%)
- **Unsubscribe Rate**: % of users disabling notifications (target <5%)

### System Metrics
- **Delivery Latency**: p50, p95, p99 from event emitted to notification sent (target <5s)
- **Delivery Success Rate**: % of notifications successfully delivered (target >99%)
- **Queue Depth**: Max queue size during peak hours
- **Error Rate**: % of failed deliveries

### User Satisfaction
- **Net Promoter Score**: Would you recommend Remedy notifications?
- **Survey**: "Notifications helped me stay on top of my resources" (1-5 scale)

## Alternative Approaches

### Approach 1: Passive Polling (No Notifications)
**Concept**: CLI command `remedy next` shows what to work on now based on context/time

**Pros**: Simple, no infrastructure, no privacy concerns
**Cons**: User must remember to check, defeats purpose

### Approach 2: Calendar Integration (No Separate System)
**Concept**: Sync time slots to Google Calendar, rely on calendar notifications

**Pros**: Leverage existing notification infrastructure
**Cons**: Can't include resource details, no priority decay, no context awareness

### Approach 3: Email-Only
**Concept**: Skip push/SMS, only email notifications + digests

**Pros**: Simple, works everywhere, no mobile app needed
**Cons**: Not real-time, easy to ignore

### Approach 4: Webhook-Only
**Concept**: Remedy emits webhooks, user configures Zapier/IFTTT to route

**Pros**: Ultimate flexibility, no notification infrastructure
**Cons**: Power users only, high barrier to entry

## Risks & Mitigations

### Risk 1: Notification Fatigue
**Risk**: Users get too many notifications, disable system
**Mitigation**:
- Aggressive throttling by default
- Smart batching (combine related events)
- Machine learning to optimize frequency per user
- Easy snooze/disable options

### Risk 2: Delivery Failures
**Risk**: Push tokens expire, emails bounce, users don't receive
**Mitigation**:
- Multi-channel fallback (push fails → try email)
- Retry logic with exponential backoff
- Dead-letter queue for manual review
- In-app "missed notifications" view

### Risk 3: Privacy Backlash
**Risk**: Users uncomfortable with server-side notification tracking
**Mitigation**:
- Client-side option (no server)
- Transparency about data collection
- GDPR compliance (right to delete)
- Opt-in, not opt-out

### Risk 4: Cross-Device Consistency
**Risk**: User dismisses notification on phone, still shows on desktop
**Mitigation**:
- Sync dismissal events via server
- Notification IDs shared across devices
- Eventual consistency acceptable (slight delay)

### Risk 5: Cost Explosion
**Risk**: Email/SMS costs grow faster than revenue
**Mitigation**:
- Free tier limits (e.g., 100 notifications/month free)
- Premium tier for unlimited ($5/month)
- SMS only for premium users
- Pre-compute digests to reduce email volume

## Recommendations

### For Immediate Implementation (Phase 1)
1. **Start with desktop notifications** via CLI daemon
   - Simplest, no server dependency
   - Use Windows/macOS native notification APIs
   - Schedule time slot reminders (T-15 minutes)

2. **Add in-app banners** in CLI
   - Show pending notifications on next command
   - No infrastructure needed

3. **Implement basic preferences**
   - Enable/disable notifications
   - Quiet hours
   - Per-time-slot toggles

### For Near-Term (Phase 2)
4. **Add email digests**
   - Weekly summary (easiest to batch)
   - Priority decay warnings (valuable, async)
   - Integrate SendGrid

### For Long-Term (Phase 3+)
5. **Mobile app + push notifications**
   - Critical for mobile-first users
   - Opens door to location-based context

6. **Context detection**
   - Calendar API integration
   - Manual context toggle

7. **Advanced analytics**
   - A/B test notification timing
   - Machine learning for optimal frequency

### Architecture Recommendation
- **Hybrid approach**: Client-side for immediacy, server-side for richness
- **Event bus**: SQLite queue locally, forward to Redis/RabbitMQ when online
- **Channels**: Desktop (now), Email (soon), Push (later)

## Conclusion

An event-driven notification system transforms Remedy from a passive CLI tool into an **active productivity partner**. By intelligently surfacing resources at the right time in the right context, the system maximizes the value of saved content and prevents priority decay from silently discarding valuable work.

The **hybrid architecture** balances offline-first principles with the richness of cloud-backed notifications. Starting with client-side desktop notifications provides immediate value, while the modular design allows graceful enhancement with email, push, and advanced context detection over time.

**Key Success Factors**:
1. Respect user attention (throttle aggressively)
2. Provide value (right notification, right time, right context)
3. Enable control (granular preferences)
4. Maintain privacy (local-first, opt-in)
5. Scale gracefully (from 10 to 100,000 users)

**Next Step**: Prototype Phase 1 (desktop notifications) to validate user value before investing in server infrastructure.

---

**Document Version**: 1.0
**Date**: 2025-11-13
**Status**: Design Concept (Not Implemented)
