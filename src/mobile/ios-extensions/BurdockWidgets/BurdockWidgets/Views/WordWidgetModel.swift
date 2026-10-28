import Foundation
import WidgetKit
import BurdockShared

/// Identity of the one widget of v1; the intent and the widget share it.
enum WordWidgetKind {
    static let value = "WordWidget"
}

/// Everything a family view needs, computed once per timeline entry (spec 04 §3.4): views never touch files.
struct WordWidgetModel {
    let entry: WordEntry?
    let translationHidden: Bool
    let palette: WidgetPalette
    let progress: DayProgress?
    let recent: [WordEntry]
    let isFreeTier: Bool

    static func make(slot: TimelineSlot, timeline: WordTimeline) -> WordWidgetModel {
        guard let entry = slot.entry else {
            return fallback(themeId: timeline.entries.first?.themeId)
        }
        return WordWidgetModel(
            entry: entry,
            translationHidden: timeline.translationMode == .hidden,
            palette: .named(entry.themeId),
            progress: TimelineSelection.dayProgress(in: timeline, current: entry, now: slot.date),
            recent: TimelineSelection.recentWords(in: timeline, before: entry),
            isFreeTier: entry.isFreeTier)
    }

    /// «Реп’яшок спить»: the timeline ran out or was never written (FR-04-8).
    static func fallback(themeId: String? = nil) -> WordWidgetModel {
        WordWidgetModel(entry: nil, translationHidden: false, palette: themeId.map(WidgetPalette.named) ?? .tsvit, progress: nil, recent: [], isFreeTier: false)
    }

    /// Placeholder, snapshot and gallery data (spec 04 §3.4); never the App Group.
    static func preview(entry: WordEntry = PreviewData.entry(), hidden: Bool = false, themeId: String = "burdock.default", progress: DayProgress? = DayProgress(shown: 3, planned: 5), freeTier: Bool = false) -> WordWidgetModel {
        WordWidgetModel(entry: entry, translationHidden: hidden, palette: .named(themeId), progress: progress, recent: PreviewData.recent(), isFreeTier: freeTier)
    }

    var isFallback: Bool { entry == nil }
    var word: String { entry?.word ?? "" }
    var ipa: String? { entry?.ipa.flatMap { $0.isEmpty ? nil : $0 } }
    var example: String? { entry?.example.flatMap { $0.isEmpty ? nil : $0 } }
    var translation: String { translationHidden ? WidgetStrings.tapToRecall : (entry?.translation ?? "") }
    var partOfSpeechShort: String? { entry?.pos.flatMap(WidgetLabels.partOfSpeechShort) }
    var partOfSpeechLong: String? { entry?.pos.flatMap(WidgetLabels.partOfSpeechLong) }
    var category: String? { entry?.categoryId.flatMap(WidgetLabels.category) }
    var progressText: String? { progress.map { WidgetStrings.progress(shown: $0.shown, planned: $0.planned) } }
    /// «5/5 сьогодні» as a pill once the free pool of the day is used up (spec 02 §3.12); never the word «Pro».
    var showsFreeBadge: Bool { isFreeTier && (progress.map { $0.shown >= $0.planned } ?? false) }
    var quizLabel: String { WidgetStrings.quiz(count: progress?.shown ?? 0) }

    func url(for family: WidgetFamily) -> URL {
        let source = DeepLinks.source(for: family)
        if let entry = entry {
            return DeepLinks.word(entry.lexemeId, source: source)
        }
        return DeepLinks.today(source: source)
    }

    var quizURL: URL { DeepLinks.quiz(source: "widget") }
}

/// `burdock://` links (spec 04 §3.4); `src` becomes the exposure surface in the app's `DeepLinkRouter`.
enum DeepLinks {
    static let scheme = "burdock"

    static func word(_ id: UUID, source: String) -> URL {
        URL(string: "\(scheme)://word/\(id.uuidString.lowercased())?src=\(source)")!
    }

    static func quiz(source: String) -> URL {
        URL(string: "\(scheme)://quiz?src=\(source)")!
    }

    static func today(source: String) -> URL {
        URL(string: "\(scheme)://today?src=\(source)")!
    }

    static func source(for family: WidgetFamily) -> String {
        "widget.\(family.sourceName)"
    }
}

extension WidgetFamily {
    /// Stable names for the marker file and deep links (the app matches `accessory*` to the lock screen surface).
    var markerName: String {
        switch self {
        case .systemSmall: return "systemSmall"
        case .systemMedium: return "systemMedium"
        case .systemLarge: return "systemLarge"
        case .systemExtraLarge: return "systemExtraLarge"
        case .accessoryRectangular: return "accessoryRectangular"
        case .accessoryCircular: return "accessoryCircular"
        case .accessoryInline: return "accessoryInline"
        @unknown default: return "unknown"
        }
    }

    var sourceName: String {
        switch self {
        case .systemSmall: return "small"
        case .systemMedium: return "medium"
        case .systemLarge: return "large"
        case .systemExtraLarge: return "extraLarge"
        default: return markerName
        }
    }

    var isAccessory: Bool {
        switch self {
        case .accessoryRectangular, .accessoryCircular, .accessoryInline: return true
        default: return false
        }
    }
}

/// Ukrainian labels for the metadata in the file (the app's glossary wording, spec 12).
enum WidgetLabels {
    static func partOfSpeechShort(_ pos: String) -> String? {
        switch pos.lowercased() {
        case "noun": return "ім."
        case "verb": return "дієсл."
        case "adjective": return "прикм."
        case "adverb": return "присл."
        case "pronoun": return "займ."
        case "preposition": return "прийм."
        case "conjunction": return "спол."
        case "determiner": return "озн."
        case "interjection": return "виг."
        case "numeral": return "числ."
        case "phrase": return "вислів"
        default: return nil
        }
    }

    static func partOfSpeechLong(_ pos: String) -> String? {
        switch pos.lowercased() {
        case "noun": return "іменник"
        case "verb": return "дієслово"
        case "adjective": return "прикметник"
        case "adverb": return "прислівник"
        case "pronoun": return "займенник"
        case "preposition": return "прийменник"
        case "conjunction": return "сполучник"
        case "determiner": return "означник"
        case "interjection": return "вигук"
        case "numeral": return "числівник"
        case "phrase": return "вислів"
        default: return nil
        }
    }

    static func category(_ slug: String) -> String? {
        switch slug.lowercased() {
        case "communication": return "Спілкування"
        case "travel": return "Подорожі"
        case "work": return "Робота і бізнес"
        case "it": return "IT та програмування"
        case "food": return "Їжа"
        case "health": return "Здоров’я"
        case "city": return "Місто й транспорт"
        case "shopping": return "Покупки"
        case "military": return "Військова справа"
        default: return nil
        }
    }
}

/// Localised strings outside SwiftUI `Text` (Localizable.xcstrings, source language uk).
enum WidgetStrings {
    static var tapToRecall: String {
        String(localized: "widget.tapToRecall", defaultValue: "Торкнись, щоб згадати")
    }

    static func progress(shown: Int, planned: Int) -> String {
        String(localized: "widget.progress", defaultValue: "\(shown)/\(planned) сьогодні")
    }

    static func quiz(count: Int) -> String {
        count > 0
            ? String(localized: "widget.quizCount", defaultValue: "Перевірити \(count) слів")
            : String(localized: "widget.quiz", defaultValue: "Перевірити")
    }
}
