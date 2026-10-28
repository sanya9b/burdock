import Foundation

/// Where the App Group files live (spec 04 §3.2.1). Paths mirror `AppGroupWidgetBridge` in the app.
public enum AppGroupPaths {
    public static let groupId = "group.org.burdock.app"
    public static let timelineFile = "Timeline/word-timeline.v1.json"
    public static let cursorFile = "Timeline/word-cursor.v1.json"
    public static let markerFile = "Timeline/extension-marker.v1.json"

    public static func containerURL(fileManager: FileManager = .default) -> URL? {
        fileManager.containerURL(forSecurityApplicationGroupIdentifier: groupId)
    }
}

/// Reads the timeline and cursor, writes the cursor and the marker atomically; a missing or unreadable file is nil, never a crash.
public struct TimelineStore {
    /// Requests closer together than this merge into one marker, so several widget instances show up as one family list.
    public static let markerMergeWindow: TimeInterval = 600

    private let root: URL
    private let fileManager: FileManager

    public init(root: URL, fileManager: FileManager = .default) {
        self.root = root
        self.fileManager = fileManager
    }

    /// The store of the App Group container, or nil when the extension has no container (entitlement missing).
    public static func appGroup(fileManager: FileManager = .default) -> TimelineStore? {
        guard let url = AppGroupPaths.containerURL(fileManager: fileManager) else { return nil }
        return TimelineStore(root: url, fileManager: fileManager)
    }

    public func loadTimeline() -> WordTimeline? {
        guard let data = read(AppGroupPaths.timelineFile) else { return nil }
        return TimelineCodec.decodeTimeline(data)
    }

    public func loadCursor() -> WordCursor? {
        guard let data = read(AppGroupPaths.cursorFile) else { return nil }
        return TimelineCodec.decodeCursor(data)
    }

    public func loadMarker() -> ExtensionMarker? {
        guard let data = read(AppGroupPaths.markerFile) else { return nil }
        return try? TimelineCodec.decoder().decode(ExtensionMarker.self, from: data)
    }

    public func saveCursor(_ cursor: WordCursor) throws {
        try write(try TimelineCodec.encoder().encode(cursor), to: AppGroupPaths.cursorFile)
    }

    public func saveMarker(_ marker: ExtensionMarker) throws {
        try write(try TimelineCodec.encoder().encode(marker), to: AppGroupPaths.markerFile)
    }

    /// `getTimeline` happened for `family`: the marker tells the app the timeline was requested after `generatedAt` (spec 04 §3.5).
    @discardableResult
    public func recordRequest(family: String, now: Date) -> ExtensionMarker {
        var families: Set<String> = [family]
        if let previous = loadMarker(), now.timeIntervalSince(previous.requestedAt) < Self.markerMergeWindow, now >= previous.requestedAt {
            families.formUnion(previous.families)
        }
        let marker = ExtensionMarker(requestedAt: now, families: families.sorted())
        try? saveMarker(marker)
        return marker
    }

    /// «Далі»: moves the cursor to the next entry and returns it, or nil when the timeline has no next word.
    @discardableResult
    public func advanceCursor(now: Date, source: String = "nextWordIntent") -> WordCursor? {
        guard let timeline = loadTimeline(), let next = TimelineSelection.nextIndex(in: timeline, cursor: loadCursor(), now: now) else {
            return nil
        }
        let cursor = WordCursor(cursorIndex: next, advancedAt: now, source: source)
        try? saveCursor(cursor)
        return cursor
    }

    private func read(_ relativePath: String) -> Data? {
        let url = root.appendingPathComponent(relativePath)
        return try? Data(contentsOf: url)
    }

    private func write(_ data: Data, to relativePath: String) throws {
        let url = root.appendingPathComponent(relativePath)
        try fileManager.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        #if os(iOS)
        try data.write(to: url, options: [.atomic, .completeFileProtectionUntilFirstUserAuthentication])
        #else
        try data.write(to: url, options: [.atomic])
        #endif
    }
}
