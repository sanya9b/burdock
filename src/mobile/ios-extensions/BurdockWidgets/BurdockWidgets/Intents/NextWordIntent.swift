import AppIntents
import WidgetKit
import BurdockShared

/// «Далі» (spec 04 §3.4, FR-04-16): runs in the extension process, moves the cursor file and reloads the word widget; a user
/// action, so the reload is not charged to the system budget. The app settles the exposure the next time it runs (§3.5).
@available(iOS 17.0, *)
struct NextWordIntent: AppIntent {
    static let title: LocalizedStringResource = "Наступне слово"
    static let isDiscoverable = false

    func perform() async throws -> some IntentResult {
        if let store = TimelineStore.appGroup() {
            store.advanceCursor(now: Date())
        }
        WidgetCenter.shared.reloadTimelines(ofKind: WordWidgetKind.value)
        return .result()
    }
}
