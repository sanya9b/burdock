import XCTest
@testable import BurdockShared

/// Decoding of the App Group documents written by the app (spec 04 §3.2.2–3.2.4).
final class TimelineCodecTests: XCTestCase {
    private let sample = """
    {"schemaVersion":1,"generatedAt":"2026-10-27T09:17:42+02:00","plannedUntil":"2026-10-29T09:17:42+02:00","cadenceMinutes":60,"translationMode":"hidden","pair":{"native":"uk","target":"en"},"fallback":{"kind":"openApp","mascotState":"sleep"},"entries":[{"entryIndex":0,"fireDate":"2026-10-27T09:00:00+02:00","plannedUntil":"2026-10-27T10:00:00+02:00","lexemeId":"0199A1C2-7F3E-7D21-9A44-2C0F5E4B8A10","word":"burdock","ipa":"/ˈbɜːdɒk/","pos":"noun","translation":"реп’ях","example":"A burdock burr stuck to my sleeve.","themeId":"burdock.default","isFreeTier":false,"categoryId":"nature","futureField":42}],"anotherFutureField":"x"}
    """

    func testDecodesTheAppsDocumentWithOffsetsAndIgnoresUnknownFields() throws {
        let timeline = try XCTUnwrap(TimelineCodec.decodeTimeline(Data(sample.utf8)))

        XCTAssertEqual(timeline.cadenceMinutes, 60)
        XCTAssertEqual(timeline.translationMode, .hidden)
        XCTAssertEqual(timeline.pair, LanguagePair(native: "uk", target: "en"))
        XCTAssertEqual(timeline.entries.count, 1)
        XCTAssertEqual(timeline.entries[0].word, "burdock")
        XCTAssertEqual(timeline.entries[0].fireDate, ISO8601DateFormatter().date(from: "2026-10-27T07:00:00Z"))
        XCTAssertEqual(timeline.generatedAt, ISO8601DateFormatter().date(from: "2026-10-27T07:17:42Z"))
    }

    func testUnknownSchemaVersionIsRejectedWithoutThrowing() {
        let tooNew = sample.replacingOccurrences(of: "\"schemaVersion\":1,\"generatedAt\"", with: "\"schemaVersion\":7,\"generatedAt\"")

        XCTAssertNil(TimelineCodec.decodeTimeline(Data(tooNew.utf8)))
        XCTAssertNil(TimelineCodec.decodeTimeline(Data("not json".utf8)))
    }

    func testUnknownTranslationModeReadsAsVisibleAndFractionalDatesParse() throws {
        let odd = sample
            .replacingOccurrences(of: "\"translationMode\":\"hidden\"", with: "\"translationMode\":\"blink\"")
            .replacingOccurrences(of: "2026-10-27T09:17:42+02:00", with: "2026-10-27T09:17:42.1234567+02:00")
        let timeline = try XCTUnwrap(TimelineCodec.decodeTimeline(Data(odd.utf8)))

        XCTAssertEqual(timeline.translationMode, .visible)
    }

    func testCursorRoundTripsThroughTheEncoder() throws {
        let cursor = WordCursor(cursorIndex: 12, advancedAt: ISO8601DateFormatter().date(from: "2026-09-27T07:12:31Z")!, source: "nextWordIntent")

        let data = try TimelineCodec.encoder().encode(cursor)
        let json = String(decoding: data, as: UTF8.self)
        let back = try XCTUnwrap(TimelineCodec.decodeCursor(data))

        XCTAssertTrue(json.contains("\"cursorIndex\":12"))
        XCTAssertTrue(json.contains("\"advancedAt\":\"2026-09-27T07:12:31Z\""))
        XCTAssertEqual(back, cursor)
    }
}
