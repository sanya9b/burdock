import Foundation

/// Built-in data for placeholders, snapshots and the gallery (spec 04 §3.4): never read from the App Group.
public enum PreviewData {
    public static let lexemeId = UUID(uuidString: "0199A1C2-7F3E-7D21-9A44-2C0F5E4B8A10")!

    public static func entry(now: Date = Date(), word: String = "burdock", translation: String = "реп’ях", example: String? = "A burdock burr stuck to my sleeve.", themeId: String = "burdock.default") -> WordEntry {
        WordEntry(
            entryIndex: 3,
            fireDate: now,
            plannedUntil: now.addingTimeInterval(3600),
            lexemeId: lexemeId,
            word: word,
            ipa: "/ˈbɜːdɒk/",
            pos: "noun",
            translation: translation,
            example: example,
            themeId: themeId,
            isFreeTier: false,
            categoryId: "travel")
    }

    public static func longEntry(now: Date = Date()) -> WordEntry {
        WordEntry(
            entryIndex: 4,
            fireDate: now,
            plannedUntil: now.addingTimeInterval(3600),
            lexemeId: lexemeId,
            word: "serendipity",
            ipa: "/ˌsɛrənˈdɪpəti/",
            pos: "noun",
            translation: "щасливий випадок",
            example: "She found the café by pure serendipity.",
            themeId: "burdock.default",
            isFreeTier: false,
            categoryId: "communication")
    }

    /// Three earlier words for the «Нещодавні» list of systemLarge.
    public static func recent(now: Date = Date()) -> [WordEntry] {
        let words: [(String, String, String)] = [("wander", "блукати", "/ˈwɒndə/"), ("itinerary", "маршрут", "/aɪˈtɪnərəri/"), ("layover", "пересадка", "/ˈleɪəʊvə/")]
        return words.enumerated().map { index, item in
            WordEntry(
                entryIndex: index,
                fireDate: now.addingTimeInterval(Double(index - 3) * 3600),
                plannedUntil: now.addingTimeInterval(Double(index - 2) * 3600),
                lexemeId: UUID(uuidString: "0199A1C2-7F3E-7D21-9A44-2C0F5E4B8A1\(index + 1)")!,
                word: item.0,
                ipa: item.2,
                pos: index == 0 ? "verb" : "noun",
                translation: item.1,
                example: nil,
                themeId: "burdock.default",
                isFreeTier: false,
                categoryId: "travel")
        }
    }

    public static func timeline(now: Date = Date()) -> WordTimeline {
        WordTimeline(
            schemaVersion: 1,
            generatedAt: now.addingTimeInterval(-4 * 3600),
            plannedUntil: now.addingTimeInterval(44 * 3600),
            cadenceMinutes: 60,
            translationMode: .visible,
            pair: LanguagePair(native: "uk", target: "en"),
            fallback: FallbackEntry(kind: "openApp", mascotState: "sleep"),
            entries: recent(now: now) + [entry(now: now), longEntry(now: now.addingTimeInterval(3600))])
    }
}
