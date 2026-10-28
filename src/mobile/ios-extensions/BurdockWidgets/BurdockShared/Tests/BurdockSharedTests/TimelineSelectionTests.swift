import XCTest
@testable import BurdockShared

/// Which word a widget shows and what the «Далі» cursor changes (spec 04 §3.2.3, FR-04-8, FR-04-16).
final class TimelineSelectionTests: XCTestCase {
    private let t0 = ISO8601DateFormatter().date(from: "2026-10-27T07:00:00Z")!
    private var kyiv: Calendar {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "Europe/Kyiv")!
        return calendar
    }

    private func timeline(entries: Int = 4, distinct: Int? = nil) -> WordTimeline {
        let ids = (0..<(distinct ?? entries)).map { _ in UUID() }
        let list = (0..<entries).map { index -> WordEntry in
            let fire = t0.addingTimeInterval(Double(index) * 3600)
            return WordEntry(entryIndex: index, fireDate: fire, plannedUntil: fire.addingTimeInterval(3600), lexemeId: ids[index % ids.count], word: "word\(index)", ipa: nil, pos: "noun", translation: "слово\(index)", example: nil, themeId: "burdock.default", isFreeTier: false, categoryId: nil)
        }
        return WordTimeline(schemaVersion: 1, generatedAt: t0, plannedUntil: t0.addingTimeInterval(Double(entries) * 3600), cadenceMinutes: 60, translationMode: .visible, pair: LanguagePair(native: "uk", target: "en"), fallback: FallbackEntry(kind: "openApp", mascotState: "sleep"), entries: list)
    }

    func testNaturalEntryIsTheLastOneWhoseTimeHasCome() {
        let tl = timeline()

        XCTAssertEqual(TimelineSelection.currentEntry(in: tl, cursor: nil, now: t0.addingTimeInterval(90 * 60))?.entryIndex, 1)
        XCTAssertNil(TimelineSelection.currentEntry(in: tl, cursor: nil, now: t0.addingTimeInterval(-60)))
        XCTAssertNil(TimelineSelection.currentEntry(in: tl, cursor: nil, now: tl.plannedUntil))
    }

    func testCursorMovesAheadUntilTheScheduleCatchesUp() {
        let tl = timeline()
        let cursor = WordCursor(cursorIndex: 2, advancedAt: t0.addingTimeInterval(10 * 60), source: "nextWordIntent")

        XCTAssertEqual(TimelineSelection.currentEntry(in: tl, cursor: cursor, now: t0.addingTimeInterval(20 * 60))?.entryIndex, 2)
        XCTAssertEqual(TimelineSelection.currentEntry(in: tl, cursor: cursor, now: t0.addingTimeInterval(3 * 3600 + 60))?.entryIndex, 3)
        let stale = WordCursor(cursorIndex: 2, advancedAt: t0.addingTimeInterval(-3600), source: "nextWordIntent")
        XCTAssertEqual(TimelineSelection.currentEntry(in: tl, cursor: stale, now: t0.addingTimeInterval(60))?.entryIndex, 0, "a cursor older than the timeline is ignored")
    }

    func testUpcomingStartsNowAndEndsWithTheFallback() {
        let tl = timeline()
        let now = t0.addingTimeInterval(90 * 60)

        let slots = TimelineSelection.upcoming(in: tl, cursor: nil, now: now)

        XCTAssertEqual(slots.first?.date, now)
        XCTAssertEqual(slots.first?.entry?.entryIndex, 1)
        XCTAssertEqual(slots.map { $0.entry?.entryIndex }, [1, 2, 3, nil])
        XCTAssertEqual(slots.last?.date, tl.plannedUntil)
        XCTAssertEqual(TimelineSelection.upcoming(in: tl, cursor: nil, now: tl.plannedUntil.addingTimeInterval(1)).count, 1)
    }

    func testBeforeTheFirstSlotTheFirstWordShowsAtOnce() {
        let tl = timeline()
        let now = t0.addingTimeInterval(-600)

        let slots = TimelineSelection.upcoming(in: tl, cursor: nil, now: now)

        XCTAssertEqual(slots.map { $0.entry?.entryIndex }, [0, 1, 2, 3, nil])
        XCTAssertEqual(slots.first?.date, now, "the first word is shown immediately, not at its slot")
        XCTAssertEqual(slots[1].date, t0.addingTimeInterval(3600))
    }

    func testNextIndexFollowsTheCurrentWordAndStopsAtTheEnd() {
        let tl = timeline()

        XCTAssertEqual(TimelineSelection.nextIndex(in: tl, cursor: nil, now: t0.addingTimeInterval(60)), 1)
        let atEnd = WordCursor(cursorIndex: 3, advancedAt: t0.addingTimeInterval(120), source: "nextWordIntent")
        XCTAssertNil(TimelineSelection.nextIndex(in: tl, cursor: atEnd, now: t0.addingTimeInterval(180)))
    }

    func testDayProgressCountsDistinctWordsOfTheLearningDay() {
        // 8 hourly slots over 4 distinct words starting 09:00 Kyiv; at the 5th slot the learner has met all 4 words.
        let tl = timeline(entries: 8, distinct: 4)
        let current = tl.entries[4]

        let progress = TimelineSelection.dayProgress(in: tl, current: current, now: current.fireDate, calendar: kyiv)
        let early = TimelineSelection.dayProgress(in: tl, current: tl.entries[1], now: tl.entries[1].fireDate, calendar: kyiv)

        XCTAssertEqual(progress, DayProgress(shown: 4, planned: 4))
        XCTAssertEqual(early, DayProgress(shown: 2, planned: 4))
    }

    func testLearningDayStartsAtFourInTheMorning() {
        let threeAm = ISO8601DateFormatter().date(from: "2026-10-27T01:00:00Z")!   // 03:00 Kyiv (UTC+2)
        let fiveAm = ISO8601DateFormatter().date(from: "2026-10-27T03:00:00Z")!    // 05:00 Kyiv

        XCTAssertEqual(TimelineSelection.learningDayStart(of: threeAm, calendar: kyiv), ISO8601DateFormatter().date(from: "2026-10-26T02:00:00Z"))
        XCTAssertEqual(TimelineSelection.learningDayStart(of: fiveAm, calendar: kyiv), ISO8601DateFormatter().date(from: "2026-10-27T02:00:00Z"))
    }

    func testRecentWordsAreDistinctAndNewestFirst() {
        let tl = timeline(entries: 6, distinct: 3)

        let recent = TimelineSelection.recentWords(in: tl, before: tl.entries[5])

        XCTAssertEqual(recent.map(\.entryIndex), [4, 3])
    }

    func testStoreAdvancesTheCursorAndMergesRequestMarkers() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let store = TimelineStore(root: root)
        let tl = timeline()
        try FileManager.default.createDirectory(at: root.appendingPathComponent("Timeline"), withIntermediateDirectories: true)
        try TimelineCodec.encoder().encode(tl).write(to: root.appendingPathComponent(AppGroupPaths.timelineFile))

        let cursor = try XCTUnwrap(store.advanceCursor(now: t0.addingTimeInterval(60)))
        store.recordRequest(family: "systemSmall", now: t0)
        let merged = store.recordRequest(family: "accessoryRectangular", now: t0.addingTimeInterval(120))
        let later = store.recordRequest(family: "systemMedium", now: t0.addingTimeInterval(3600))

        XCTAssertEqual(cursor.cursorIndex, 1)
        XCTAssertEqual(store.loadCursor(), cursor)
        XCTAssertEqual(merged.families, ["accessoryRectangular", "systemSmall"])
        XCTAssertEqual(later.families, ["systemMedium"], "requests further apart than the merge window start a fresh marker")
        XCTAssertEqual(store.loadMarker(), later)
        XCTAssertEqual(store.loadTimeline()?.entries.count, 4)
    }

    func testLockScreenTextFitting() {
        XCTAssertEqual(WidgetText.circular("burdock"), "burdock")
        XCTAssertNil(WidgetText.circular("serendipity"))
        XCTAssertNil(WidgetText.circular("call off"))
        XCTAssertEqual(WidgetText.inline(word: "burdock", translation: "реп’ях"), "burdock — реп’ях")
        let long = WidgetText.inline(word: "serendipity", translation: "щасливий випадок, який стався несподівано", limit: 32)
        XCTAssertLessThanOrEqual(long.count, 32)
        XCTAssertTrue(long.hasPrefix("serendipity — "))
        XCTAssertTrue(long.hasSuffix("…"))
    }
}
