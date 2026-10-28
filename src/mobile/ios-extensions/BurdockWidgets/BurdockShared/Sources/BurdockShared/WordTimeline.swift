import Foundation

/// Whether surfaces show the translation next to the word (spec 06 «спочатку вгадай»); unknown values read as visible.
public enum TranslationMode: String, Codable, Sendable {
    case visible
    case hidden

    public init(from decoder: Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        self = TranslationMode(rawValue: raw.lowercased()) ?? .visible
    }
}

public struct LanguagePair: Codable, Sendable, Equatable {
    public let native: String
    public let target: String

    public init(native: String, target: String) {
        self.native = native
        self.target = target
    }
}

/// What to show once the timeline is exhausted (FR-04-8); the extension localises the text, the file only names the state.
public struct FallbackEntry: Codable, Sendable, Equatable {
    public let kind: String
    public let mascotState: String

    public init(kind: String, mascotState: String) {
        self.kind = kind
        self.mascotState = mascotState
    }
}

/// One slot of the timeline (spec 04 §3.2.2). Unknown fields are ignored for forward compatibility.
public struct WordEntry: Codable, Sendable, Identifiable, Equatable {
    public var id: Int { entryIndex }
    public let entryIndex: Int
    public let fireDate: Date
    public let plannedUntil: Date
    public let lexemeId: UUID
    public let word: String
    public let ipa: String?
    public let pos: String?
    public let translation: String
    public let example: String?
    public let themeId: String
    public let isFreeTier: Bool
    public let categoryId: String?

    public init(entryIndex: Int, fireDate: Date, plannedUntil: Date, lexemeId: UUID, word: String, ipa: String?, pos: String?, translation: String, example: String?, themeId: String, isFreeTier: Bool, categoryId: String?) {
        self.entryIndex = entryIndex
        self.fireDate = fireDate
        self.plannedUntil = plannedUntil
        self.lexemeId = lexemeId
        self.word = word
        self.ipa = ipa
        self.pos = pos
        self.translation = translation
        self.example = example
        self.themeId = themeId
        self.isFreeTier = isFreeTier
        self.categoryId = categoryId
    }
}

/// `Timeline/word-timeline.v1.json`, written by the app (spec 04 §3.2.2).
public struct WordTimeline: Codable, Sendable, Equatable {
    public static let currentSchemaVersion = 1

    public let schemaVersion: Int
    public let generatedAt: Date
    public let plannedUntil: Date
    public let cadenceMinutes: Int
    public let translationMode: TranslationMode
    public let pair: LanguagePair
    public let fallback: FallbackEntry
    public let entries: [WordEntry]

    public init(schemaVersion: Int, generatedAt: Date, plannedUntil: Date, cadenceMinutes: Int, translationMode: TranslationMode, pair: LanguagePair, fallback: FallbackEntry, entries: [WordEntry]) {
        self.schemaVersion = schemaVersion
        self.generatedAt = generatedAt
        self.plannedUntil = plannedUntil
        self.cadenceMinutes = cadenceMinutes
        self.translationMode = translationMode
        self.pair = pair
        self.fallback = fallback
        self.entries = entries
    }
}

/// `Timeline/word-cursor.v1.json` (spec 04 §3.2.3), written by the «Далі» intent and read by both sides.
public struct WordCursor: Codable, Sendable, Equatable {
    public let schemaVersion: Int
    public let cursorIndex: Int
    public let advancedAt: Date
    public let source: String

    public init(schemaVersion: Int = 1, cursorIndex: Int, advancedAt: Date, source: String) {
        self.schemaVersion = schemaVersion
        self.cursorIndex = cursorIndex
        self.advancedAt = advancedAt
        self.source = source
    }
}

/// `Timeline/extension-marker.v1.json` (spec 04 §3.2.1): proof for the app that a widget asked for the timeline.
public struct ExtensionMarker: Codable, Sendable, Equatable {
    public let schemaVersion: Int
    public let requestedAt: Date
    public let families: [String]

    public init(schemaVersion: Int = 1, requestedAt: Date, families: [String]) {
        self.schemaVersion = schemaVersion
        self.requestedAt = requestedAt
        self.families = families
    }
}

/// Encoding and decoding with ISO-8601 dates; a schema the extension does not know yields nil instead of a crash (spec 04 §3.2.4).
public enum TimelineCodec {
    public static func decoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let value = try decoder.singleValueContainer().decode(String.self)
            if let date = TimelineCodec.parseDate(value) {
                return date
            }
            throw DecodingError.dataCorrupted(DecodingError.Context(codingPath: decoder.codingPath, debugDescription: "Unsupported date \(value)"))
        }
        return decoder
    }

    public static func encoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.sortedKeys]
        return encoder
    }

    public static func decodeTimeline(_ data: Data) -> WordTimeline? {
        struct Probe: Decodable { let schemaVersion: Int }
        guard let probe = try? JSONDecoder().decode(Probe.self, from: data), probe.schemaVersion >= 1, probe.schemaVersion <= WordTimeline.currentSchemaVersion else {
            return nil
        }
        return try? decoder().decode(WordTimeline.self, from: data)
    }

    public static func decodeCursor(_ data: Data) -> WordCursor? {
        try? decoder().decode(WordCursor.self, from: data)
    }

    /// ISO-8601 with or without fractional seconds, with an offset or Z.
    public static func parseDate(_ value: String) -> Date? {
        let plain = ISO8601DateFormatter()
        plain.formatOptions = [.withInternetDateTime]
        if let date = plain.date(from: value) {
            return date
        }
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return fractional.date(from: value)
    }
}
