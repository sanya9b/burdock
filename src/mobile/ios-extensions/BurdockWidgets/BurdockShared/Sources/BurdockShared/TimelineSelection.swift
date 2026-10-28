import Foundation

/// One WidgetKit entry: a word from the given date, or the fallback when `entry` is nil.
public struct TimelineSlot: Equatable, Sendable {
    public let date: Date
    public let entry: WordEntry?

    public init(date: Date, entry: WordEntry?) {
        self.date = date
        self.entry = entry
    }
}

/// «N/M сьогодні»: distinct words of the learning day shown so far (current included) over distinct words planned for it.
public struct DayProgress: Equatable, Sendable {
    public let shown: Int
    public let planned: Int

    public init(shown: Int, planned: Int) {
        self.shown = shown
        self.planned = planned
    }
}

/// Pure rules for turning the file into what a widget shows (spec 04 §3.2.3, §3.3.2, FR-04-8, FR-04-16).
public enum TimelineSelection {
    /// The learning day switches at 04:00 local time (CLAUDE.md §4), the same rule as `LearningDay` in the app.
    public static let learningDayStartHour = 4

    /// The entry whose time has come, unless the «Далі» cursor already moved past it.
    public static func currentEntry(in timeline: WordTimeline, cursor: WordCursor?, now: Date) -> WordEntry? {
        guard now < timeline.plannedUntil else { return nil }
        let natural = timeline.entries.last(where: { $0.fireDate <= now })
        guard let cursor = cursor, cursor.advancedAt <= now, cursor.advancedAt >= timeline.generatedAt else {
            return natural
        }
        guard let pointed = timeline.entries.first(where: { $0.entryIndex == cursor.cursorIndex }) else {
            return natural
        }
        if let natural = natural, natural.entryIndex >= pointed.entryIndex {
            return natural
        }
        return pointed
    }

    /// Entries for WidgetKit from `now` on: the current word now, the future words at their times, then the fallback at `plannedUntil`.
    public static func upcoming(in timeline: WordTimeline, cursor: WordCursor?, now: Date) -> [TimelineSlot] {
        guard now < timeline.plannedUntil else {
            return [TimelineSlot(date: now, entry: nil)]
        }
        var slots: [TimelineSlot] = []
        // Before the first slot (or when today is already over its caps) the first upcoming word shows right away:
        // WidgetKit would otherwise keep the placeholder, which reads as a broken widget.
        let current = currentEntry(in: timeline, cursor: cursor, now: now) ?? timeline.entries.first(where: { $0.fireDate > now })
        if let current = current {
            slots.append(TimelineSlot(date: now, entry: current))
        }
        let threshold = current?.entryIndex ?? -1
        for entry in timeline.entries where entry.entryIndex > threshold && entry.fireDate > now {
            slots.append(TimelineSlot(date: entry.fireDate, entry: entry))
        }
        slots.append(TimelineSlot(date: timeline.plannedUntil, entry: nil))
        return slots
    }

    /// Index the «Далі» button moves to, or nil when nothing follows the current word.
    public static func nextIndex(in timeline: WordTimeline, cursor: WordCursor?, now: Date) -> Int? {
        let currentIndex = currentEntry(in: timeline, cursor: cursor, now: now)?.entryIndex ?? -1
        return timeline.entries.first(where: { $0.entryIndex > currentIndex })?.entryIndex
    }

    /// Start of the learning day that contains `date` (04:00 local).
    public static func learningDayStart(of date: Date, calendar: Calendar = .current) -> Date {
        let midnight = calendar.startOfDay(for: date)
        let start = calendar.date(byAdding: .hour, value: learningDayStartHour, to: midnight) ?? midnight
        if date < start {
            return calendar.date(byAdding: .day, value: -1, to: start) ?? start
        }
        return start
    }

    /// Counter for the system families: words of the current learning day up to `current` over all words planned for the day.
    public static func dayProgress(in timeline: WordTimeline, current: WordEntry, now: Date, calendar: Calendar = .current) -> DayProgress {
        let start = learningDayStart(of: now, calendar: calendar)
        let end = calendar.date(byAdding: .day, value: 1, to: start) ?? start.addingTimeInterval(86_400)
        var planned = Set<UUID>()
        var shown = Set<UUID>()
        for entry in timeline.entries where entry.fireDate >= start && entry.fireDate < end {
            planned.insert(entry.lexemeId)
            if entry.entryIndex <= current.entryIndex {
                shown.insert(entry.lexemeId)
            }
        }
        planned.insert(current.lexemeId)
        shown.insert(current.lexemeId)
        return DayProgress(shown: shown.count, planned: planned.count)
    }

    /// Distinct words that came before `current`, newest first (systemLarge «Нещодавні»).
    public static func recentWords(in timeline: WordTimeline, before current: WordEntry, limit: Int = 3) -> [WordEntry] {
        var seen: Set<UUID> = [current.lexemeId]
        var result: [WordEntry] = []
        for entry in timeline.entries.reversed() where entry.entryIndex < current.entryIndex {
            if seen.insert(entry.lexemeId).inserted {
                result.append(entry)
                if result.count == limit {
                    break
                }
            }
        }
        return result
    }
}

/// Text fitting for the lock screen families (spec 02 §3.12).
public enum WidgetText {
    public static let circularMaxLetters = 7

    /// Word for `accessoryCircular`, or nil when it does not fit and the glyph should show instead.
    public static func circular(_ word: String) -> String? {
        let trimmed = word.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.count <= circularMaxLetters && !trimmed.contains(" ") ? trimmed : nil
    }

    /// `accessoryInline`: «word — translation», the translation shortened first.
    public static func inline(word: String, translation: String, limit: Int = 32) -> String {
        let full = "\(word) — \(translation)"
        if full.count <= limit {
            return full
        }
        let room = limit - word.count - 4
        if room >= 4 {
            return "\(word) — \(translation.prefix(room))…"
        }
        return String(word.prefix(limit))
    }
}
